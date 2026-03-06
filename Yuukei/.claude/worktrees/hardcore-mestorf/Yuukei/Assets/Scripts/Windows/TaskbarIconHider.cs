using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

[System.Obsolete("Use PlatformBootstrapper + IAppIntegration instead")]
public class TaskbarIconHider : MonoBehaviour
{
    // --- Windows API ---
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    // --- Constants ---
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;

    // SetWindowPos Flags
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020; // OSにスタイルの変更を通知し、再描画を強制する

    private IEnumerator Start()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        // UniWindowControllerの初期化完了を確実に待つための遅延
        // ※環境に合わせてフレーム待ち、または秒数待ちを行います
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        HideFromTaskbar();
#else
        yield break;
#endif
    }

    private void HideFromTaskbar()
    {
        // 起動直後のGetActiveWindowはフォーカス外れで失敗するリスクがあるため、
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
            
            // ★重要：OSに変更を通知し、タスクバー・Alt+Tabから即座に除外させる
            SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, 
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }
        else
        {
            Debug.LogError("Failed to get window handle. Taskbar hide operation aborted.");
        }
    }
}