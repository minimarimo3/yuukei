// ==========================================================================
// PluginLoader.cs
// Yuukei.Plugin.Contracts.dll を実装した DLL を動的ロードするコンポーネント（§7）。
// UNITY_STANDALONE 専用。AOT環境（Android / iOS）では除外する。
// ==========================================================================

#if UNITY_STANDALONE

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Yuukei.Plugin.Contracts インターフェースを実装した DLL を動的ロードする（§7）。
/// ロードしたプラグインを TriggerManager / DaihonFunctionRegistry に登録する。
/// 禁止: AssemblyLoadContext.Unload() を実装すること（§7.4参照）。
/// </summary>
public class PluginLoader : MonoBehaviour
{
    public static PluginLoader Instance { get; private set; }

    private const string PendingUnloadFileName = "pending_unload.json";

    // パッケージID → ロードしたアセンブリ一覧
    private readonly Dictionary<string, List<Assembly>> _loadedAssemblies =
        new Dictionary<string, List<Assembly>>();

    // 次回起動時にロードしないパッケージIDセット
    private HashSet<string> _pendingUnloadPackageIds = new HashSet<string>();

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
        // pending_unload.json を読み込んでアンロード対象を除外リストに追加（§7.4, §16参照）
        LoadPendingUnloadList();

        // Plugins/ ディレクトリのDLLをロード（グローバルプラグイン）
        var globalPluginsPath = Path.Combine(Application.persistentDataPath, "Plugins");
        if (Directory.Exists(globalPluginsPath))
        {
            LoadFromDirectory(globalPluginsPath);
        }
    }

    // ── 公開API ─────────────────────────────────────────────────────────

    /// <summary>
    /// ディレクトリ内の全DLLをスキャンしてロードする（§7.3）。
    /// ITriggerPlugin 実装は TriggerManager に、IDaihonFunction 実装は DaihonFunctionRegistry に自動登録する。
    /// ロード失敗した DLL は Debug.LogError を出してスキップし、残りのDLLの処理を続行する（§7.3）。
    /// </summary>
    public void LoadFromDirectory(string dirPath, string packageId = null)
    {
        if (!Directory.Exists(dirPath))
        {
            Debug.LogWarning($"[PluginLoader] ディレクトリが見つかりません: {dirPath}");
            return;
        }

        // アンロード対象のパッケージはスキップ
        if (packageId != null && _pendingUnloadPackageIds.Contains(packageId))
        {
            Debug.Log($"[PluginLoader] パッケージ「{packageId}」はアンロード予約済みのためスキップします。");
            return;
        }

        var dllFiles = Directory.GetFiles(dirPath, "*.dll", SearchOption.AllDirectories);
        foreach (var dllPath in dllFiles)
        {
            LoadDll(dllPath, packageId);
        }
    }

    /// <summary>
    /// 指定パッケージのプラグインを「次回起動時にロードしない」対象として記録する（§7.3, §7.4）。
    /// アンロードは即座には行わない。
    /// </summary>
    public void MarkForUnloadOnRestart(string packageId)
    {
        _pendingUnloadPackageIds.Add(packageId);
        SavePendingUnloadList();
        Debug.Log("[PluginLoader] プラグインの変更はアプリを再起動後に反映されます。");
    }

    // ── 内部処理 ──────────────────────────────────────────────────────────

    private void LoadDll(string dllPath, string packageId)
    {
        Assembly assembly;
        try
        {
            // Assembly.LoadFrom を使用（フュージョンポリシーの適用とストロングネームチェックが有効）
            assembly = Assembly.LoadFrom(dllPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PluginLoader] DLLのロードに失敗しました: {dllPath}\n{e.Message}");
            return;
        }

        // パッケージIDに紐付けて記録
        if (packageId != null)
        {
            if (!_loadedAssemblies.TryGetValue(packageId, out var list))
            {
                list = new List<Assembly>();
                _loadedAssemblies[packageId] = list;
            }
            list.Add(assembly);
        }

        // アセンブリ内の ITriggerPlugin / IDaihonFunction 実装を検索して登録
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            Debug.LogError($"[PluginLoader] 型の読み込みに失敗しました: {dllPath}\n{e.Message}");
            // ロードできた型だけで続行
            types = e.Types;
        }

        foreach (var type in types)
        {
            if (type == null || type.IsAbstract || type.IsInterface) continue;

            try
            {
                RegisterTypeIfPlugin(type);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PluginLoader] 型「{type.FullName}」の登録に失敗: {e.Message}");
            }
        }

        Debug.Log($"[PluginLoader] DLLをロードしました: {Path.GetFileName(dllPath)}");
    }

    private void RegisterTypeIfPlugin(Type type)
    {
        if (typeof(ITriggerPlugin).IsAssignableFrom(type))
        {
            var plugin = (ITriggerPlugin)Activator.CreateInstance(type);
            TriggerManager.Instance?.RegisterPlugin(plugin);
            Debug.Log($"[PluginLoader] ITriggerPlugin「{plugin.TriggerName}」を登録しました。");
        }
        else if (typeof(IDaihonFunction).IsAssignableFrom(type))
        {
            var func = (IDaihonFunction)Activator.CreateInstance(type);
            DaihonFunctionRegistry.Register(func);
            Debug.Log($"[PluginLoader] IDaihonFunction「{func.FunctionName}」を登録しました。");
        }
    }

    private void LoadPendingUnloadList()
    {
        var path = Path.Combine(Application.persistentDataPath, PendingUnloadFileName);
        if (!File.Exists(path)) return;

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<PendingUnloadData>(json);
            if (data?.packageIds != null)
            {
                _pendingUnloadPackageIds = new HashSet<string>(data.packageIds);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PluginLoader] pending_unload.json の読み込みに失敗: {e.Message}");
        }
    }

    private void SavePendingUnloadList()
    {
        var path = Path.Combine(Application.persistentDataPath, PendingUnloadFileName);
        try
        {
            var data = new PendingUnloadData
            {
                packageIds = new List<string>(_pendingUnloadPackageIds)
            };
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[PluginLoader] pending_unload.json の保存に失敗: {e.Message}");
        }
    }

    [Serializable]
    private class PendingUnloadData
    {
        public List<string> packageIds;
    }
}

#endif
