using System;
using System.Reflection;
using UnityEngine;
using System.IO;

public class ExplorerItemScanner : MonoBehaviour
{
    [Header("重ねて表示したいオブジェクト（Cubeなど）")]
    public Transform targetObject; 
    
    [Header("探したいファイル/フォルダ名")]
    public string targetName = "テスト";

    private bool _isInitialized = false;
    private Assembly _uiaClient;
    private Assembly _uiaTypes;

    void Start()
    {
        // DLLの依存関係エラーを防ぐため、足りないファイルがあれば自動で探す魔法陣をセット
        AppDomain.CurrentDomain.AssemblyResolve += ResolveWPFAssembly;

        try
        {
            // Windowsのシステムフォルダの場所
            string baseDir = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\";
            if (!Directory.Exists(baseDir))
            {
                baseDir = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\WPF\"; // 念のため32bit用
            }

            // プロジェクト外にあるDLLを、実行時にステルスで読み込む
            _uiaClient = Assembly.LoadFrom(Path.Combine(baseDir, "UIAutomationClient.dll"));
            _uiaTypes = Assembly.LoadFrom(Path.Combine(baseDir, "UIAutomationTypes.dll"));
            _isInitialized = true;
            
            Debug.Log("UIAutomationのステルス起動に成功しました！");
        }
        catch (Exception e)
        {
            Debug.LogError("初期化エラー: " + e.Message);
        }
    }

    // 足りないシステムDLL（PresentationCoreなど）を要求されたら、Windowsから直接渡してあげる関数
    private Assembly ResolveWPFAssembly(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name;
        string[] searchDirs = {
            @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\",
            @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\",
            @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\WPF\",
            @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\"
        };

        foreach (var dir in searchDirs)
        {
            string path = Path.Combine(dir, name + ".dll");
            if (File.Exists(path)) return Assembly.LoadFrom(path);
        }
        return null;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_isInitialized) ScanAndPlace(targetName);
            else Debug.LogWarning("UIAutomationがまだ準備できていません。");
        }
    }

    public void ScanAndPlace(string name)
    {
        try
        {
            Debug.Log($"「{name}」をスキャン中...");

            // DLLの型を動的に取得する
            Type autoElementType = _uiaClient.GetType("System.Windows.Automation.AutomationElement");
            Type propConditionType = _uiaClient.GetType("System.Windows.Automation.PropertyCondition");
            Type treeScopeType = _uiaTypes.GetType("System.Windows.Automation.TreeScope");

            // 検索準備
            object rootElement = autoElementType.GetProperty("RootElement").GetValue(null);
            object nameProperty = autoElementType.GetField("NameProperty").GetValue(null);
            object condition = Activator.CreateInstance(propConditionType, new object[] { nameProperty, name });
            object treeScope = Enum.Parse(treeScopeType, "Descendants");

            // 実行
            object foundElement = autoElementType.GetMethod("FindFirst").Invoke(rootElement, new object[] { treeScope, condition });

            if (foundElement != null)
            {
                // 結果の座標を取得
                object current = autoElementType.GetProperty("Current").GetValue(foundElement);
                object rect = current.GetType().GetProperty("BoundingRectangle").GetValue(current);

                Type rectType = rect.GetType();
                double left = (double)rectType.GetProperty("Left").GetValue(rect);
                double top = (double)rectType.GetProperty("Top").GetValue(rect);
                double width = (double)rectType.GetProperty("Width").GetValue(rect);
                double height = (double)rectType.GetProperty("Height").GetValue(rect);

                Debug.Log($"発見! 座標: X={left}, Y={top}, 幅={width}, 高さ={height}");
                MoveTargetToScreenRect((float)left, (float)top, (float)width, (float)height);
            }
            else
            {
                Debug.LogWarning($"「{name}」が見つかりませんでした。");
            }
        }
        catch (Exception e)
        {
            // 動的呼び出し時のエラーは InnerException に詳細が入る
            Debug.LogError($"エラー: {e.InnerException?.Message ?? e.Message}\n{e.StackTrace}");
        }
    }

    void MoveTargetToScreenRect(float left, float top, float width, float height)
    {
        if (targetObject == null || Camera.main == null) return;

        float centerX = left + width / 2f;
        float centerY = top + height / 2f;

        float unityScreenY = Screen.currentResolution.height - centerY;

        Vector3 screenPos = new Vector3(centerX, unityScreenY, 3f);
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

        targetObject.position = worldPos;
    }
}