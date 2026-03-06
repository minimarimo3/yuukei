using System.IO;
using System.IO.Compression;
using Cysharp.Threading.Tasks;
using Kirurobo;
using UnityEngine;

[RequireComponent(typeof(UniWindowController))]
public class FileDropRouter : MonoBehaviour
{
    private UniWindowController _windowController;

    private void Awake()
    {
        _windowController = GetComponent<UniWindowController>();
        _windowController.allowDropFiles = true;
        _windowController.OnDropFiles += HandleFilesDropped;
    }

    private void OnDestroy()
    {
        if (_windowController != null)
        {
            _windowController.OnDropFiles -= HandleFilesDropped;
        }
    }

    private void HandleFilesDropped(string[] filePaths)
    {
        foreach (string path in filePaths)
        {
            if (!File.Exists(path)) continue;
            RouteFileAsync(path).Forget();
        }
    }

    private async UniTaskVoid RouteFileAsync(string path)
    {
        string extension = Path.GetExtension(path).ToLower();
        string fileName = Path.GetFileNameWithoutExtension(path);

        // 拡張子に基づいて適切なマネージャーにルーティング（§14.5）
        switch (extension)
        {
            case ".vrm":
            case ".bundle":
                Debug.Log($"[FileDropRouter] キャラクターファイルがドロップされました: {path}");
                if (ConfigManager.Instance != null)
                {
                    ConfigManager.Instance.AddCharacter(fileName, path);
                    // 即座に選択状態にする
                    var added = ConfigManager.Instance.Settings.savedCharacters
                        .Find(c => c.filePath == path);
                    if (added != null)
                        ConfigManager.Instance.SetCurrentCharacter(added.id);
                }
                break;

            case ".zip":
                await HandleZipDropAsync(path);
                break;

            case ".daihon":
            case ".txt":
                //  単体台本のインポート（§14.5）
                HandleDaihonDrop(path);
                break;

            default:
                Debug.LogWarning($"[FileDropRouter] 未対応のファイル形式: {extension}");
                break;
        }

        // トリガーマネージャーにもファイルドロップを通知（§8.2 FileDropTrigger）
        TriggerManager.Instance?.OnFilesDropped(new[] { path });
    }

    private async UniTask HandleZipDropAsync(string path)
    {
        // ZIPのルートに package.json が存在するか確認
        bool hasPackageJson = false;
        bool hasThemeJson = false;
        bool hasPlugins = false;

        try
        {
            using var archive = ZipFile.OpenRead(path);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName == "package.json") hasPackageJson = true;
                if (entry.FullName == "theme.json") hasThemeJson = true;
                if (entry.FullName.StartsWith("plugins/") && entry.FullName.EndsWith(".dll"))
                    hasPlugins = true;
                // 早期終了
                if (hasPackageJson && hasThemeJson && hasPlugins) break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FileDropRouter] ZIPの読み込みに失敗しました: {e.Message}");
            return;
        }

        if (hasPackageJson)
        {
            // plugins/ を含む場合はセキュリティ警告UI表示（§7.5参照）
            if (hasPlugins)
            {
                bool confirmed = await ShowPluginSecurityWarningAsync();
                if (!confirmed)
                {
                    Debug.Log("[FileDropRouter] ユーザーがプラグインのインストールをキャンセルしました。");
                    return;
                }
            }

            if (PackageManager.Instance != null)
            {
                await PackageManager.Instance.InstallAsync(path);
            }
            return;
        }

        if (hasThemeJson)
        {
            if (ThemeManager.Instance != null)
            {
                await ThemeManager.Instance.LoadThemeFromZipAsync(path);
            }
            return;
        }

        Debug.LogWarning("[FileDropRouter] パッケージでもテーマでもないZIPです。");
    }

    /// <summary>
    /// プラグインのセキュリティ警告ダイアログを表示し、ユーザーの確認を待つ（§7.5）。
    /// </summary>
    private async UniTask<bool> ShowPluginSecurityWarningAsync()
    {
        if (PopupManager.Instance == null)
        {
            Debug.LogWarning("[FileDropRouter] PopupManager が見つかりません。プラグインのインストールを続行します。");
            return true;
        }

        bool confirmed = false;
        bool answered = false;

        PopupManager.Instance.ShowWarning(
            "このパッケージにはプラグイン(DLL)が含まれています。信頼できる配布元からのみインストールしてください。",
            onConfirm: () => { confirmed = true; answered = true; },
            onCancel: () => { confirmed = false; answered = true; }
        );

        // ユーザーが回答するまで待機
        while (!answered)
        {
            await UniTask.Yield();
        }

        return confirmed;
    }

    private void HandleDaihonDrop(string sourcePath)
    {
        string fileName = Path.GetFileName(sourcePath);
        string targetDir = Path.Combine(Application.persistentDataPath, "Daihon");
        
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        string destPath = Path.Combine(targetDir, fileName);

        try
        {
            File.Copy(sourcePath, destPath, true); // 上書き許可
            Debug.Log($"[FileDropRouter] 台本をインストールしました: {destPath}");

            // DaihonScenarioManagerに変更を通知して再読み込みさせる
            if (DaihonScenarioManager.Instance != null)
            {
                DaihonScenarioManager.Instance.LoadLocalScripts();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FileDropRouter] 台本のコピーに失敗しました: {e.Message}");
        }
    }
}