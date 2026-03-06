using System;
using System.IO;
using System.IO.Compression; // .NET Standard 2.1互換レベルで利用可能
using UnityEngine;

public class ThemeManager : MonoBehaviour
{
    public static ThemeManager Instance { get; private set; }

    // テーマが読み込まれた時に発火するイベント
    // 引数1: パースされたテーマデータ, 引数2: 画像ファイルが格納されているディレクトリパス
    public event Action<ThemeSettings, string> OnThemeLoaded;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// ドロップされたZIPを展開し、テーマを適用します。
    /// </summary>
    public void LoadThemeFromZip(string zipFilePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(zipFilePath);
        string extractPath = Path.Combine(Application.persistentDataPath, "Themes", fileName);

        // 同名テーマのディレクトリが既に存在する場合は、安全のために中身をクリアして上書き
        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, true);
        }
        Directory.CreateDirectory(extractPath);

        try
        {
            // ZIP展開
            ZipFile.ExtractToDirectory(zipFilePath, extractPath);
            Debug.Log($"ZIP展開成功: {extractPath}");
            
            LoadAndBroadcastTheme(extractPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"テーマの展開・適用に失敗しました: {e.Message}");
        }
    }

    private void LoadAndBroadcastTheme(string dirPath)
    {
        string jsonPath = Path.Combine(dirPath, "theme.json");
        if (!File.Exists(jsonPath))
        {
            Debug.LogError("ZIP内に theme.json が見つかりません。");
            return;
        }

        string jsonString = File.ReadAllText(jsonPath);
        ThemeSettings themeSettings = JsonUtility.FromJson<ThemeSettings>(jsonString);

        // イベントを発火して、登録されている各UIコンポーネントに通知
        OnThemeLoaded?.Invoke(themeSettings, dirPath);
    }
}