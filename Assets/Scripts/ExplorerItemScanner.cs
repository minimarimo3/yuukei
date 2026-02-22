using System;
using System.Diagnostics;
using System.Threading;
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

    private Process scannerProcess;
    private CancellationTokenSource cts;

    // メインスレッドでの処理用変数
    private bool hasNewData = false;
    private float newLeft, newTop, newWidth, newHeight;
    private readonly object dataLock = new object();

    void Start()
    {
        cts = new CancellationTokenSource();
        StartScannerProcess();
    }

    void StartScannerProcess()
    {
        UnityEngine.Debug.Log($"「{targetName}」のスキャンプロセスを起動します...");

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = scannerExePath,
            Arguments = $"\"{targetName}\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            CreateNoWindow = true
        };

        try
        {
            scannerProcess = Process.Start(psi);

            if (scannerProcess != null)
            {
                // 非同期で標準出力と標準エラー出力を読み取る
                _ = ReadOutputAsync(scannerProcess, cts.Token);
                _ = ReadErrorAsync(scannerProcess, cts.Token);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"外部アプリの起動自体に失敗しました: {e.Message}");
        }
    }

    async Task ReadOutputAsync(Process process, CancellationToken token)
    {
        try
        {
            while (!process.HasExited && !token.IsCancellationRequested)
            {
                string line = await process.StandardOutput.ReadLineAsync();
                if (line == null) break;
                
                HandleOutputLine(line);
            }
        }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested)
                UnityEngine.Debug.LogError($"出力の読み取り中にエラーが発生しました: {e.Message}");
        }
    }

    async Task ReadErrorAsync(Process process, CancellationToken token)
    {
        try
        {
            while (!process.HasExited && !token.IsCancellationRequested)
            {
                string line = await process.StandardError.ReadLineAsync();
                if (line == null) break;

                UnityEngine.Debug.LogError($"[外部アプリ エラー出力]: {line}");
            }
        }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested)
                UnityEngine.Debug.LogError($"エラー出力の読み取り中にエラーが発生しました: {e.Message}");
        }
    }

    void HandleOutputLine(string output)
    {
        output = output.Trim();
        if (string.IsNullOrEmpty(output)) return;

        if (output.StartsWith("SUCCESS:"))
        {
            string data = output.Substring(8);
            string[] parts = data.Split(',');

            if (parts.Length == 4 &&
                float.TryParse(parts[0], out float left) &&
                float.TryParse(parts[1], out float top) &&
                float.TryParse(parts[2], out float width) &&
                float.TryParse(parts[3], out float height))
            {
                lock (dataLock)
                {
                    newLeft = left;
                    newTop = top;
                    newWidth = width;
                    newHeight = height;
                    hasNewData = true;
                }
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning($"スキャン結果: {output}");
        }
    }

    void Update()
    {
        // メインスレッドで座標更新を行う
        bool shouldUpdate = false;
        float l = 0, t = 0, w = 0, h = 0;

        lock (dataLock)
        {
            if (hasNewData)
            {
                shouldUpdate = true;
                l = newLeft;
                t = newTop;
                w = newWidth;
                h = newHeight;
                hasNewData = false;
            }
        }

        if (shouldUpdate)
        {
            UnityEngine.Debug.Log($"発見! 座標変化: X={l}, Y={t}, 幅={w}, 高さ={h}");
            MoveTargetToScreenRect(l, t, w, h);
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

    void OnDestroy()
    {
        CleanupProcess();
    }

    void OnApplicationQuit()
    {
        CleanupProcess();
    }

    void CleanupProcess()
    {
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }

        if (scannerProcess != null && !scannerProcess.HasExited)
        {
            try
            {
                // UiaScanner側で exit を受け取って終了するようになっているため、まず標準入力に書き込む
                scannerProcess.StandardInput.WriteLine("exit");
                scannerProcess.StandardInput.Close();
                
                // 少し待って終了しなかったら強制終了する
                if (!scannerProcess.WaitForExit(1000))
                {
                    scannerProcess.Kill();
                }
            }
            catch
            {
                // すでに終了している場合などの例外は無視
            }
            finally
            {
                scannerProcess.Dispose();
                scannerProcess = null;
            }
        }
    }
}