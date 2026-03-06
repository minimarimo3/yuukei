// ==========================================================================
// TriggerManager.cs
// 全トリガーの登録・監視・発火を一元管理するシングルトン。
// ==========================================================================

using System;
using System.Collections.Generic;
using Daihon;
using UnityEngine;

/// <summary>
/// 全トリガーの登録・監視・発火を一元管理するシングルトン。
/// 組み込みトリガーを ITriggerPlugin として内部実装する。
/// 発生したトリガーイベントは DaihonScenarioManager などへブロードキャストする。
/// </summary>
public class TriggerManager : MonoBehaviour
{
    public static TriggerManager Instance { get; private set; }

    /// <summary>
    /// トリガーイベント発火をシステム全体へ通知するためのC#イベント。
    /// DaihonScenarioManager がこれを監視して、台本の実行を判断する。
    /// </summary>
    public event Action<TriggerPayload> OnSystemEventFired;

    // 登録済みプラグイン（TriggerName → ITriggerPlugin）
    private readonly Dictionary<string, ITriggerPlugin> _plugins =
        new Dictionary<string, ITriggerPlugin>();

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

    /// <summary>トリガープラグインを登録する。</summary>
    public void RegisterPlugin(ITriggerPlugin plugin)
    {
        if (plugin == null) return;
        _plugins[plugin.TriggerName] = plugin;
        plugin.OnFired += OnTriggerFired;
    }

    /// <summary>トリガー監視を開始する。</summary>
    public void StartMonitoring()
    {
        _isMonitoring = true;
    }

    /// <summary>トリガー監視を停止する。</summary>
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

    // ── トリガー発火処理 ─────────────────────────────────────────

    private void OnTriggerFired(TriggerPayload payload)
    {
        if (!_isMonitoring) return;

        // システムイベントとして外部（DaihonScenarioManager等）へブロードキャスト
        OnSystemEventFired?.Invoke(payload);
    }
}
