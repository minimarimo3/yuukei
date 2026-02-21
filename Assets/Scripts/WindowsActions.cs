using System.Runtime.InteropServices; // Windows APIを使うため
using System.Diagnostics; // アプリ起動のため
using UnityEngine;
using Debug = UnityEngine.Debug;

public class WindowsActions
{
    // --- Windows API の定義 (呪文のようなもの) ---
    
    // 入力(マウス・キーボード)をブロックする機能
    [DllImport("user32.dll")]
    private static extern bool BlockInput(bool fBlockIt);

    // ------------------------------------------

    /// <summary>
    /// マウスとキーボードの操作を無効化/有効化します
    /// ※注意: 管理者権限で実行しないと動きません
    /// </summary>
    public void SetInputState(bool enable)
    {
        bool block = !enable;
        Debug.Log($"Windows操作ロック: {block}");
        
        // 実際にWindowsに命令を送る
        bool success = BlockInput(block);
        
        if (!success)
        {
            Debug.LogWarning("操作のブロックに失敗しました。Unityを「管理者として実行」していますか？");
        }
    }

    /// <summary>
    /// 指定したパスのアプリケーションを開きます
    /// </summary>
    public void OpenApplication(string path)
    {
        Debug.Log($"アプリ起動: {path}");
        try
        {
            // Windowsの機能でファイルやアプリを開く
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (System.Exception e)
        {
            Debug.LogError($"アプリが開けませんでした: {path}\nエラー: {e.Message}");
        }
    }
}