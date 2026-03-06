using System;
using System.IO;
using System.IO.Compression; // .NET Standard 2.1互換レベルで利用可能
using Cysharp.Threading.Tasks;
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
    /// ZIPファイルを展開してテーマを適用する（§5.2）。
    /// 同名ディレクトリが存在する場合は削除してから上書き展開する。
    /// </summary>
    public async UniTask LoadThemeFromZipAsync(string zipFilePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(zipFilePath);
        string extractPath = Path.Combine(Application.persistentDataPath, "Themes", fileName);

        // 同名テーマのディレクトリが既に存在する場合は削除して上書き（§5.1）
        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, true);
        }
        Directory.CreateDirectory(extractPath);

        try
        {
            await UniTask.RunOnThreadPool(() =>
                ZipFile.ExtractToDirectory(zipFilePath, extractPath));
            Debug.Log($"[ThemeManager] ZIP展開成功: {extractPath}");

            await LoadThemeFromDirectoryAsync(extractPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ThemeManager] テーマの展開・適用に失敗しました: {e.Message}");
        }
    }

    /// <summary>
    /// 既に展開済みのディレクトリからテーマを適用する（§5.2）。
    /// PackageManager がパッケージ内 theme/ を適用する際に使う。
    /// </summary>
    public async UniTask LoadThemeFromDirectoryAsync(string dirPath)
    {
        string jsonPath = Path.Combine(dirPath, "theme.json");
        if (!File.Exists(jsonPath))
        {
            Debug.LogWarning($"[ThemeManager] theme.json が見つかりません: {jsonPath}");
            return;
        }

        string jsonString;
        try
        {
            jsonString = File.ReadAllText(jsonPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ThemeManager] theme.json の読み込みに失敗しました: {e.Message}");
            return;
        }

        ThemeSettings themeSettings;
        try
        {
            themeSettings = JsonUtility.FromJson<ThemeSettings>(jsonString);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ThemeManager] theme.json のデシリアライズに失敗しました: {e.Message}");
            return;
        }

        // イベントを発火して、登録されている各UIコンポーネントに通知
        OnThemeLoaded?.Invoke(themeSettings, dirPath);

        // テクスチャの非同期ロードを待機する（§5.4参照 - テクスチャロードは各UIコンポーネント側で行う）
        await UniTask.Yield();
    }

    /// <summary>
    /// 後方互換のために残す同期版（非推奨）。
    /// 代わりに LoadThemeFromZipAsync を使うこと。
    /// </summary>
    [Obsolete("LoadThemeFromZipAsync を使用してください。")]
    public void LoadThemeFromZip(string zipFilePath)
    {
        LoadThemeFromZipAsync(zipFilePath).Forget();
    }
}