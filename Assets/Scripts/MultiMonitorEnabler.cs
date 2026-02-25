using UnityEngine;
using System;
using System.Runtime.InteropServices;

public class MultiMonitorEnabler : MonoBehaviour
{
    // --- Windows API ---
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    // 仮想スクリーン（全モニターを囲む領域）の情報を取得する定数
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    private const uint SWP_NOZORDER = 0x0004;

    void Start()
    {
        // エディタ上では動作させず、ビルドしたexeでのみ実行します
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        SetupMultiMonitor();
#endif
    }

    public void SetupMultiMonitor()
    {
        // 1. 全モニターを合わせた領域の開始座標(X, Y)と、全体の幅(Width)・高さ(Height)を取得
        int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int w = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int h = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        // 2. 現在アクティブなUnityのウィンドウハンドルを取得
        IntPtr hWnd = GetActiveWindow();

        // 3. Unityのウィンドウを全モニターサイズに拡張する
        SetWindowPos(hWnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER);
    }
}