using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
using System.Windows.Forms;
using Application = UnityEngine.Application;

[System.Obsolete("Use PlatformBootstrapper + IAppIntegration instead")]
public class SystemTrayManager : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("StreamingAssetsフォルダ内の.icoファイル名")]
    [SerializeField] private string iconFileName = "tray_icon.ico";
    [SerializeField] private string applicationName = "Mascot App";
    [SerializeField] private SettingsUIManager _settingsUI;

    private NotifyIcon _notifyIcon;
    private Thread _trayThread;
    
    // 別スレッドからメインスレッドへ処理を委譲するためのキュー
    private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

    void Start()
    {
        // エディタ上ではシステムトレイのテストは原則行わない（エディタ全体が巻き込まれるリスクがあるため）
        if (Application.isEditor)
        {
            Debug.Log("System Tray icon will only be visible in the built application.");
            return;
        }

        // デスクトップマスコットの必須設定：バックグラウンドでも動作し続けるようにする
        Application.runInBackground = true;

        InitializeTrayIcon();
    }

    void Update()
    {
        // キューに溜まったアクションをUnityのメインスレッドで実行
        while (_mainThreadActions.TryDequeue(out var action))
        {
            action?.Invoke();
        }
    }

    private void InitializeTrayIcon()
    {
        // STA(Single-Threaded Apartment)モデルでスレッドを起動
        _trayThread = new Thread(TrayThreadMain)
        {
            IsBackground = true
        };
        _trayThread.SetApartmentState(ApartmentState.STA);
        _trayThread.Start();
    }

    private void TrayThreadMain()
    {
        _notifyIcon = new NotifyIcon();

        string iconPath = System.IO.Path.Combine(Application.streamingAssetsPath, iconFileName);
        
        try
        {
            // Windows標準のアイコンを使用
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            // TODO: アイコンを作成する
            // _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Icon load failed: {e.Message}. Path: {iconPath}");
            return;
        }

        _notifyIcon.Text = applicationName;
        _notifyIcon.Visible = true;

        // コンテキストメニューの構築
        ContextMenuStrip menu = new ContextMenuStrip();

        // 1. 「設定」メニュー
        ToolStripMenuItem settingsItem = new ToolStripMenuItem("設定");
        settingsItem.Click += (sender, e) =>
        {
            // UnityのUI操作はメインスレッドで行う必要があるためキューに入れる
            _mainThreadActions.Enqueue(OpenSettings);
        };
        menu.Items.Add(settingsItem);

        menu.Items.Add(new ToolStripSeparator());

        // 2. 「終了」メニュー
        ToolStripMenuItem exitItem = new ToolStripMenuItem("終了");
        exitItem.Click += (sender, e) =>
        {
            _mainThreadActions.Enqueue(Application.Quit);
        };
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = menu;

        // メッセージループを開始（このスレッドはここでブロックされ、UIイベントを待機し続ける）
        System.Windows.Forms.Application.Run();
    }

    private void OpenSettings()
    {
        Debug.Log("設定画面を展開します");
        // TODO: ここにUnity側の設定画面（Canvas）を表示する処理を記述
        // 例: SettingsPanel.SetActive(true);
        // 必要に応じて、UniWindowControllerを使用してウィンドウを最前面に持ってくる処理を呼ぶ
        if (_settingsUI != null)
        {
            _settingsUI.ShowSettings();
        }
        else
        {
            Debug.LogError("SettingsUIManager の参照が設定されていません。");
        }
    }

    private void OnDestroy()
    {
        // アプリケーション終了時にトレイアイコンを確実に削除する（ゴーストアイコン対策）
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        // メッセージループを終了させ、スレッドを安全に閉じる
        if (_trayThread != null && _trayThread.IsAlive)
        {
            System.Windows.Forms.Application.ExitThread();
        }
    }
}