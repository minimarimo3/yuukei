#if UNITY_STANDALONE_WIN
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using System.Windows.Forms;
using Application = UnityEngine.Application;
using Debug = UnityEngine.Debug;

/// <summary>
/// Windows固有の実装。SystemTrayManager / TaskbarIconHider / MultiMonitorEnabler の
/// ロジックを統合した IAppIntegration 実装。
/// </summary>
public class WindowsAppIntegration : IAppIntegration
{
    // --- P/Invoke: タスクバー非表示 (TaskbarIconHider由来) ---
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    // --- P/Invoke: マルチモニター (MultiMonitorEnabler由来) ---
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    // --- タスクバー定数 ---
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;

    // --- SetWindowPos 定数 ---
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    // --- マルチモニター定数 ---
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    // --- トレイアイコン ---
    private readonly string _iconFileName;
    private readonly string _applicationName;
    private NotifyIcon _notifyIcon;
    private Thread _trayThread;
    private readonly ConcurrentQueue<System.Action> _mainThreadQueue = new ConcurrentQueue<System.Action>();
    private bool _disposed;

    public WindowsAppIntegration(string iconFileName = "tray_icon.ico", string applicationName = "Mascot App")
    {
        _iconFileName = iconFileName;
        _applicationName = applicationName;
    }

    public void InitializeTray(System.Action onSettingsRequested, System.Action onQuitRequested)
    {
        if (Application.isEditor)
        {
            Debug.Log("[WindowsAppIntegration] System Tray icon will only be visible in the built application.");
            return;
        }

        Application.runInBackground = true;

        _trayThread = new Thread(() => TrayThreadMain(onSettingsRequested, onQuitRequested))
        {
            IsBackground = true
        };
        _trayThread.SetApartmentState(ApartmentState.STA);
        _trayThread.Start();
    }

    private void TrayThreadMain(System.Action onSettingsRequested, System.Action onQuitRequested)
    {
        _notifyIcon = new NotifyIcon();

        string iconPath = Path.Combine(Application.streamingAssetsPath, _iconFileName);

        try
        {
            // Windows標準のアイコンを使用
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            // TODO: カスタムアイコンを作成後に以下を有効化
            // _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[WindowsAppIntegration] Icon load failed: {e.Message}. Path: {iconPath}");
            return;
        }

        _notifyIcon.Text = _applicationName;
        _notifyIcon.Visible = true;

        // コンテキストメニューの構築
        ContextMenuStrip menu = new ContextMenuStrip();

        ToolStripMenuItem settingsItem = new ToolStripMenuItem("設定");
        settingsItem.Click += (sender, e) =>
        {
            _mainThreadQueue.Enqueue(onSettingsRequested);
        };
        menu.Items.Add(settingsItem);

        menu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem exitItem = new ToolStripMenuItem("終了");
        exitItem.Click += (sender, e) =>
        {
            _mainThreadQueue.Enqueue(onQuitRequested);
        };
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = menu;

        // メッセージループを開始（このスレッドはここでブロックされ、UIイベントを待機し続ける）
        System.Windows.Forms.Application.Run();
    }

    public void HideFromTaskbar()
    {
        if (Application.isEditor) return;

        // プロダクト名から確実にウィンドウハンドルを取得する
        IntPtr hWnd = FindWindow(null, Application.productName);

        if (hWnd == IntPtr.Zero)
        {
            hWnd = GetActiveWindow(); // フォールバック
        }

        if (hWnd != IntPtr.Zero)
        {
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

            // タスクバー表示属性を消し、ツールウィンドウ属性を付与
            exStyle &= ~WS_EX_APPWINDOW;
            exStyle |= WS_EX_TOOLWINDOW;

            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle);

            // OSに変更を通知し、タスクバー・Alt+Tabから即座に除外させる
            SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }
        else
        {
            Debug.LogWarning("[WindowsAppIntegration] Failed to get window handle. Taskbar hide operation aborted.");
        }
    }

    public void SetupMultiMonitor()
    {
        if (Application.isEditor) return;

        // 全モニターを合わせた領域の開始座標(X, Y)と、全体の幅(Width)・高さ(Height)を取得
        int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int w = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int h = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        // 確実にこのUnityプロセスのメインウィンドウを取得する
        IntPtr hWnd = Process.GetCurrentProcess().MainWindowHandle;

        if (hWnd == IntPtr.Zero)
        {
            Debug.LogWarning("[WindowsAppIntegration] ウィンドウハンドルの取得に失敗しました。");
            return;
        }

        // Unityのウィンドウを全モニターサイズに拡張する
        SetWindowPos(hWnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER);
    }

    public void Tick()
    {
        while (_mainThreadQueue.TryDequeue(out var action))
        {
            action?.Invoke();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        if (_trayThread != null && _trayThread.IsAlive)
        {
            System.Windows.Forms.Application.ExitThread();
        }
    }
}
#endif
