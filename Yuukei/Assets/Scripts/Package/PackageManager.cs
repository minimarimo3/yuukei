// ==========================================================================
// PackageManager.cs
// パッケージ（.yuupkg = ZIP）のインストール・アンインストール・一覧管理（§6）。
// ==========================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// パッケージ（.yuupkg = ZIP）のインストール・アンインストール・一覧管理（§6）。
/// インストール後に ThemeManager / TriggerManager / PluginLoader に委譲する。
/// </summary>
public class PackageManager : MonoBehaviour
{
    public static PackageManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 起動時の初期化は BootSequenceCoordinator に委譲
    }

    public async UniTask RestorePackagesAsync()
    {
        if (ConfigManager.Instance?.Settings?.installedPackages == null) return;

        foreach (var pkg in ConfigManager.Instance.Settings.installedPackages)
        {
            await RestorePackageAsync(pkg);
        }
    }

    // ── 公開API ─────────────────────────────────────────────────────────

    /// <summary>
    /// パッケージをインストールする（§6.4）。
    /// 同じIDのパッケージが既にインストール済みの場合はアンインストールしてから再インストールする。
    /// </summary>
    public async UniTask InstallAsync(string packageFilePath)
    {
        if (!File.Exists(packageFilePath))
        {
            Debug.LogWarning($"[PackageManager] パッケージファイルが見つかりません: {packageFilePath}");
            return;
        }

        // package.json を ZIP から読み込む
        PackageJson packageJson;
        try
        {
            packageJson = ReadPackageJsonFromZip(packageFilePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PackageManager] package.json の読み込みに失敗しました: {e.Message}");
            return;
        }

        if (packageJson == null || string.IsNullOrEmpty(packageJson.id))
        {
            Debug.LogError("[PackageManager] package.json の id フィールドが空です。");
            return;
        }

        // 同じIDが既にインストール済みの場合はアンインストール
        var existing = ConfigManager.Instance?.Settings?.installedPackages
            .Find(p => p.id == packageJson.id);
        if (existing != null)
        {
            Uninstall(packageJson.id);
        }

        // ZIP を展開
        var installPath = Path.Combine(Application.persistentDataPath, "Packages", packageJson.id);
        if (Directory.Exists(installPath))
        {
            Directory.Delete(installPath, true);
        }
        Directory.CreateDirectory(installPath);

        try
        {
            await UniTask.RunOnThreadPool(() =>
                ZipFile.ExtractToDirectory(packageFilePath, installPath));
            Debug.Log($"[PackageManager] パッケージを展開しました: {installPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PackageManager] ZIP展開に失敗しました: {e.Message}");
            return;
        }

        // PackageInfo を生成して ConfigManager に登録
        var info = new PackageInfo
        {
            id = packageJson.id,
            name = packageJson.name,
            version = packageJson.version,
            installPath = installPath,
            enabled = true
        };
        ConfigManager.Instance.AddPackage(info);

        // theme/ を適用
        var themePath = Path.Combine(installPath, "theme");
        if (Directory.Exists(themePath) && ThemeManager.Instance != null)
        {
            await ThemeManager.Instance.LoadThemeFromDirectoryAsync(themePath);
        }

        // scripts/triggers.json を登録
        var triggersJsonPath = Path.Combine(installPath, "scripts", "triggers.json");
        if (File.Exists(triggersJsonPath) && TriggerManager.Instance != null)
        {
            TriggerManager.Instance.RegisterPackageTriggers(packageJson.id, installPath);
        }

        // plugins/ を読み込む
        var pluginsPath = Path.Combine(installPath, "plugins");
        if (Directory.Exists(pluginsPath))
        {
#if UNITY_STANDALONE
            if (PluginLoader.Instance != null)
            {
                PluginLoader.Instance.LoadFromDirectory(pluginsPath, packageJson.id);
            }
#else
            Debug.Log("[PackageManager] プラグインはPC版専用のためスキップします。");
#endif
        }

        Debug.Log($"[PackageManager] パッケージ「{packageJson.name}」のインストールが完了しました。");
    }

    /// <summary>パッケージをアンインストールする（§6.5）。</summary>
    public void Uninstall(string packageId)
    {
        var pkg = ConfigManager.Instance?.Settings?.installedPackages.Find(p => p.id == packageId);
        if (pkg == null)
        {
            Debug.LogWarning($"[PackageManager] パッケージ「{packageId}」がインストールされていません。");
            return;
        }

        // トリガー登録解除
        TriggerManager.Instance?.UnregisterPackageTriggers(packageId);

#if UNITY_STANDALONE
        // プラグインのアンロード予約
        PluginLoader.Instance?.MarkForUnloadOnRestart(packageId);
#endif

        // ディレクトリ削除
        if (Directory.Exists(pkg.installPath))
        {
            try
            {
                Directory.Delete(pkg.installPath, true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PackageManager] ディレクトリの削除に失敗: {e.Message}");
            }
        }

        // ConfigManager から削除
        ConfigManager.Instance.RemovePackage(packageId);
        Debug.Log($"[PackageManager] パッケージ「{packageId}」をアンインストールしました。");
    }

    /// <summary>インストール済みパッケージ一覧を返す（§6.3）。</summary>
    public IReadOnlyList<PackageInfo> GetInstalledPackages()
    {
        return ConfigManager.Instance?.Settings?.installedPackages
            ?? new List<PackageInfo>() as IReadOnlyList<PackageInfo>;
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────

    /// <summary>起動時に既インストールパッケージを再適用する。</summary>
    private async UniTask RestorePackageAsync(PackageInfo pkg)
    {
        if (!Directory.Exists(pkg.installPath))
        {
            Debug.LogWarning($"[PackageManager] パッケージディレクトリが見つかりません: {pkg.installPath}");
            return;
        }

        var themePath = Path.Combine(pkg.installPath, "theme");
        if (Directory.Exists(themePath) && ThemeManager.Instance != null)
        {
            await ThemeManager.Instance.LoadThemeFromDirectoryAsync(themePath);
        }

        var triggersJsonPath = Path.Combine(pkg.installPath, "scripts", "triggers.json");
        if (File.Exists(triggersJsonPath) && TriggerManager.Instance != null)
        {
            TriggerManager.Instance.RegisterPackageTriggers(pkg.id, pkg.installPath);
        }

        var pluginsPath = Path.Combine(pkg.installPath, "plugins");
        if (Directory.Exists(pluginsPath))
        {
#if UNITY_STANDALONE
            if (PluginLoader.Instance != null)
            {
                PluginLoader.Instance.LoadFromDirectory(pluginsPath, pkg.id);
            }
#else
            Debug.Log("[PackageManager] プラグインはPC版専用のためスキップします。");
#endif
        }
    }

    private static PackageJson ReadPackageJsonFromZip(string zipFilePath)
    {
        using var archive = ZipFile.OpenRead(zipFilePath);
        var entry = archive.GetEntry("package.json");
        if (entry == null)
            throw new InvalidOperationException("package.json が ZIP内に見つかりません。");

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonUtility.FromJson<PackageJson>(json);
    }

    // ── JSON デシリアライズ用クラス ────────────────────────────────────────

    [Serializable]
    private class PackageJson
    {
        public string id;
        public string name;
        public string version;
        public string author;
        public string description;
        public string minAppVersion;
    }
}
