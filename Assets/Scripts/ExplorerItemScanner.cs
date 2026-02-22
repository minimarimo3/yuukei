using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

public class ExplorerItemScanner : MonoBehaviour
{
    [Header("重ねて表示したいオブジェクト（Cubeなど）")]
    public Transform targetObject; 
    
    [Header("探したいファイル/フォルダ名")]
    public string targetName = "テスト";

    [Header("先ほど作成した UiaScanner.exe のフルパス")]
    public string scannerExePath = @"C:\Users\minimarimo3\Workspace\UiaScanner\bin\Release\net8.0-windows\win-x64\publish\UiaScanner.exe"; // ※実際のパスに書き換えてください

    async void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            await ScanAndPlaceAsync(targetName);
        }
    }

    public async Task ScanAndPlaceAsync(string name)
    {
        UnityEngine.Debug.Log($"「{name}」をスキャン中...");

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = scannerExePath,
            Arguments = $"\"{name}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true, // 【追加】標準エラー出力を受け取る
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,  // 【追加】エラーもUTF-8で受け取る
            CreateNoWindow = true
        };

        try
        {
            using (Process process = Process.Start(psi))
            {
                // 出力とエラーの両方を同時に読み取る
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(outputTask, errorTask);
                process.WaitForExit();

                string output = outputTask.Result;
                string error = errorTask.Result;

                // もしエラー出力があればUnityのコンソールに赤文字で出す
                if (!string.IsNullOrWhiteSpace(error))
                {
                    UnityEngine.Debug.LogError($"[外部アプリ エラー出力]:\n{error}");
                }

                ParseAndMove(output.Trim());
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"外部アプリの起動自体に失敗しました: {e.Message}");
        }
    }

    void ParseAndMove(string output)
    {
        // C#コンソールアプリからの出力内容に応じて処理
        if (output.StartsWith("SUCCESS:"))
        {
            string data = output.Substring(8); // "SUCCESS:" の後を取り出す
            string[] parts = data.Split(',');

            if (parts.Length == 4 &&
                float.TryParse(parts[0], out float left) &&
                float.TryParse(parts[1], out float top) &&
                float.TryParse(parts[2], out float width) &&
                float.TryParse(parts[3], out float height))
            {
                UnityEngine.Debug.Log($"発見! 座標: X={left}, Y={top}, 幅={width}, 高さ={height}");
                MoveTargetToScreenRect(left, top, width, height);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning($"スキャン結果: {output}");
        }
    }

    void MoveTargetToScreenRect(float left, float top, float width, float height)
    {
        if (targetObject == null || Camera.main == null) return;

        float centerX = left + width / 2f;
        float centerY = top + height / 2f;

        float unityScreenY = Screen.currentResolution.height - centerY;

        Vector3 screenPos = new Vector3(centerX, unityScreenY, 3f); // 3f はカメラからの距離
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

        // CyberDiveController がアタッチされているか確認して実行
        var diveController = targetObject.GetComponent<CyberDiveController>();
        if (diveController != null)
        {
            // アニメーションを開始し、完了後にフォルダを開くなどの処理を繋げる
            diveController.StartDive(worldPos, () => 
            {
                UnityEngine.Debug.Log("ダイブ完了！ここにフォルダを開く処理などを入れます。");
                // TODO: 実際のフォルダパスを特定して WindowsActions.OpenApplication() を呼ぶ
            });
        }
        else
        {
            // スクリプトがない場合は今まで通り瞬時移動
            targetObject.position = worldPos;
        }
    }
}