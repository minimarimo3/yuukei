// ==========================================================================
// TriggerManager.cs
// 全トリガーの登録・監視・発火を一元管理するシングルトン（§8）。
// ==========================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Daihon;
using Daihon.Unity;
using UnityEngine;

/// <summary>
/// 全トリガーの登録・監視・発火を一元管理するシングルトン（§8）。
/// 組み込みトリガーを ITriggerPlugin として内部実装し、パッケージトリガーも管理する。
/// 台本実行中のトリガーは多重実行制御で破棄する（§8.4）。
/// </summary>
public class TriggerManager : MonoBehaviour
{
    public static TriggerManager Instance { get; private set; }

    [SerializeField] private YuukeiActionHandler actionHandler;

    // 登録済みプラグイン（TriggerName → ITriggerPlugin）
    private readonly Dictionary<string, ITriggerPlugin> _plugins =
        new Dictionary<string, ITriggerPlugin>();

    // パッケージID → トリガーバインディングリスト
    private readonly Dictionary<string, List<TriggerBinding>> _packageBindings =
        new Dictionary<string, List<TriggerBinding>>();

    // パッケージIDスコープの変数ストア
    private readonly Dictionary<string, SimpleVariableStore> _variableStores =
        new Dictionary<string, SimpleVariableStore>();

    // 多重実行制御フラグ（§8.4）
    private bool _isScriptRunning;

    // 組み込みトリガー
    private TimeTrigger _timeTrigger;
    private ClickTrigger _clickTrigger;
    private SystemEventTrigger _systemEventTrigger;
    private FileDropTrigger _fileDropTrigger;

    private bool _isMonitoring;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 組み込み時間変数を RegisterDynamicGetter で登録（§9.3参照）
        // パッケージIDのない共有ストアに登録する
        var sharedStore = GetOrCreateVariableStore("__shared__");
        RegisterBuiltinTimeVariables(sharedStore);

        // 組み込みトリガーを生成して登録
        _timeTrigger = new TimeTrigger();
        _clickTrigger = new ClickTrigger();
        _systemEventTrigger = new SystemEventTrigger();
        _fileDropTrigger = new FileDropTrigger();

        RegisterPlugin(_timeTrigger);
        RegisterPlugin(_clickTrigger);
        RegisterPlugin(_systemEventTrigger);
        RegisterPlugin(_fileDropTrigger);

        // 監視の開始は BootSequenceCoordinator に委譲
        // StartMonitoring();
    }

    private void Update()
    {
        if (!_isMonitoring) return;

        _timeTrigger?.Tick();
        _systemEventTrigger?.Tick(Time.deltaTime);
    }

    private void OnDestroy()
    {
        StopMonitoring();
        foreach (var plugin in _plugins.Values)
        {
            plugin?.Dispose();
        }
        _plugins.Clear();
    }

    // ── 公開API ─────────────────────────────────────────────────────────

    /// <summary>トリガープラグインを登録する（§8.3）。</summary>
    public void RegisterPlugin(ITriggerPlugin plugin)
    {
        if (plugin == null) return;
        _plugins[plugin.TriggerName] = plugin;
        plugin.OnFired += OnTriggerFired;
    }

    /// <summary>
    /// パッケージの triggers.json を読み込んでトリガーを登録する（§8.3）。
    /// </summary>
    public void RegisterPackageTriggers(string packageId, string packageInstallPath)
    {
        var triggersJsonPath = Path.Combine(packageInstallPath, "scripts", "triggers.json");
        if (!File.Exists(triggersJsonPath))
        {
            Debug.LogWarning($"[TriggerManager] triggers.json が見つかりません: {triggersJsonPath}");
            return;
        }

        TriggersJsonRoot root;
        try
        {
            var json = File.ReadAllText(triggersJsonPath);
            root = JsonUtility.FromJson<TriggersJsonRoot>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TriggerManager] triggers.json のパースに失敗: {e.Message}");
            return;
        }

        if (root?.triggers == null) return;

        var bindings = new List<TriggerBinding>();
        foreach (var entry in root.triggers)
        {
            if (string.IsNullOrEmpty(entry.type) || string.IsNullOrEmpty(entry.script))
                continue;

            var binding = new TriggerBinding
            {
                packageId = packageId,
                triggerType = entry.type,
                scriptRelativePath = entry.script,
                @params = entry.@params
            };
            bindings.Add(binding);
        }

        _packageBindings[packageId] = bindings;
        Debug.Log($"[TriggerManager] パッケージ「{packageId}」のトリガーを{bindings.Count}件登録しました。");
    }

    /// <summary>パッケージのトリガーをすべて登録解除する（§8.3）。</summary>
    public void UnregisterPackageTriggers(string packageId)
    {
        _packageBindings.Remove(packageId);
        _variableStores.Remove(packageId);
    }

    /// <summary>トリガー監視を開始する（§8.3）。</summary>
    public void StartMonitoring()
    {
        _isMonitoring = true;
    }

    /// <summary>トリガー監視を停止する（§8.3）。</summary>
    public void StopMonitoring()
    {
        _isMonitoring = false;
    }

    // ── キャラクタークリックイベント ─────────────────────────────────────

    /// <summary>キャラクターがクリックされたときに外部から呼ぶ。</summary>
    public void OnCharacterClicked()
    {
        _clickTrigger?.Fire();
    }

    /// <summary>ファイルがドロップされたときに外部から呼ぶ。</summary>
    public void OnFilesDropped(string[] filePaths)
    {
        _fileDropTrigger?.OnFilesDropped(filePaths);
    }

    // ── トリガー発火処理（§8.4）─────────────────────────────────────────

    private void OnTriggerFired(TriggerPayload payload)
    {
        if (!_isMonitoring) return;

        // 多重実行制御（§8.4）
        if (_isScriptRunning)
        {
            Debug.Log($"[TriggerManager] 台本実行中のため、トリガー「{payload.TriggerName}」を破棄します。");
            return;
        }

        // 該当するバインディングを検索
        foreach (var kvp in _packageBindings)
        {
            var packageId = kvp.Key;
            var bindings = kvp.Value;

            // パッケージが有効かチェック
            var packageInfo = ConfigManager.Instance?.Settings?.installedPackages
                .Find(p => p.id == packageId);
            if (packageInfo != null && !packageInfo.enabled)
                continue;

            foreach (var binding in bindings)
            {
                if (binding.triggerType != payload.TriggerName)
                    continue;

                var installPath = packageInfo?.installPath;
                if (string.IsNullOrEmpty(installPath))
                    continue;

                var scriptPath = Path.Combine(installPath, binding.scriptRelativePath);
                if (!File.Exists(scriptPath))
                {
                    Debug.LogWarning($"[TriggerManager] 台本ファイルが見つかりません: {scriptPath}");
                    continue;
                }

                ExecuteScriptAsync(packageId, scriptPath).Forget();
                // 多重実行制御（§8.4）: 一致した最初のバインディングのみ実行し、残りは破棄する。
                // 仕様「キャラクターは一度に一つのことしかしない」に基づく意図的な設計。
                return;
            }
        }
    }

    private async UniTaskVoid ExecuteScriptAsync(string packageId, string scriptPath)
    {
        _isScriptRunning = true;
        try
        {
            string scriptText;
            try
            {
                scriptText = File.ReadAllText(scriptPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TriggerManager] 台本ファイルの読み込みに失敗: {e.Message}");
                return;
            }

            var store = GetOrCreateVariableStore(packageId);
            var handler = actionHandler != null ? actionHandler : FindObjectOfType<YuukeiActionHandler>();

            if (handler == null)
            {
                Debug.LogWarning("[TriggerManager] YuukeiActionHandler が見つかりません。");
                return;
            }

            await DaihonRunner.RunAsync(scriptText, handler, store);
        }
        finally
        {
            _isScriptRunning = false;
        }
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────

    private SimpleVariableStore GetOrCreateVariableStore(string packageId)
    {
        if (!_variableStores.TryGetValue(packageId, out var store))
        {
            store = new SimpleVariableStore();
            RegisterBuiltinTimeVariables(store);
            _variableStores[packageId] = store;
        }
        return store;
    }

    private static void RegisterBuiltinTimeVariables(SimpleVariableStore store)
    {
        store.RegisterDynamicGetter("年",   () => DaihonValue.FromNumber(DateTime.Now.Year));
        store.RegisterDynamicGetter("月",   () => DaihonValue.FromNumber(DateTime.Now.Month));
        store.RegisterDynamicGetter("日",   () => DaihonValue.FromNumber(DateTime.Now.Day));
        store.RegisterDynamicGetter("時",   () => DaihonValue.FromNumber(DateTime.Now.Hour));
        store.RegisterDynamicGetter("分",   () => DaihonValue.FromNumber(DateTime.Now.Minute));
        store.RegisterDynamicGetter("秒",   () => DaihonValue.FromNumber(DateTime.Now.Second));
        store.RegisterDynamicGetter("ミリ秒", () => DaihonValue.FromNumber(DateTime.Now.Millisecond));
        store.RegisterDynamicGetter("曜日", () => DaihonValue.FromString(
            new[] { "日", "月", "火", "水", "木", "金", "土" }[(int)DateTime.Now.DayOfWeek]));
        store.RegisterDynamicGetter("週",   () => DaihonValue.FromNumber(
            System.Globalization.CultureInfo.CurrentCulture.Calendar
                .GetWeekOfYear(DateTime.Now,
                    System.Globalization.CalendarWeekRule.FirstDay,
                    DayOfWeek.Monday)));
    }

    // ── JSON デシリアライズ用クラス ────────────────────────────────────────

    [Serializable]
    private class TriggersJsonRoot
    {
        public List<TriggerEntry> triggers;
    }

    [Serializable]
    private class TriggerEntry
    {
        public string type;
        public TriggerParams @params;
        public string script;
    }

    [Serializable]
    private class TriggerParams
    {
        public int hour;
        public int minute;
        public string[] days;
        public float cpuThreshold;
        public float batteryThreshold;
        public string[] extensions;
        public string targetName;
    }

    private class TriggerBinding
    {
        public string packageId;
        public string triggerType;
        public string scriptRelativePath;
        public TriggerParams @params;
    }
}
