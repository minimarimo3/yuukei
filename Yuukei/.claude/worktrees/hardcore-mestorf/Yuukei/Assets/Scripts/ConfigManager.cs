using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// --- データ構造の定義 (§2) ---
[Serializable]
public class CharacterData
{
    public string id;        // Guid.NewGuid().ToString() で生成
    public string name;      // ファイル名（拡張子なし）をデフォルト値とする
    public string filePath;  // モデルファイルやアセットバンドルの絶対パス
}

[Serializable]
public class PackageInfo
{
    public string id;           // package.json の "id" フィールド
    public string name;
    public string version;
    public string installPath;  // persistentDataPath/Packages/{id}/
    public bool enabled;        // false の場合 TriggerManager はトリガーをスキップする
}

[Serializable]
public class LLMSettings
{
    public string provider = "none"; // "none" | "cloud" | "local"
    public string endpointUrl = "";  // クラウド: APIエンドポイント, ローカル: OllamaのURL
    public string modelName = "";
    // NOTE: APIキーはここには保存しない。ICredentialStorage を使う（§3参照）。
}

[Serializable]
public class AppSettings
{
    public string currentCharacterId = "";
    public List<CharacterData> savedCharacters = new List<CharacterData>();
    public string activePackageId = "";
    public List<PackageInfo> installedPackages = new List<PackageInfo>();
    public LLMSettings llmSettings = new LLMSettings();
}

// --- 管理クラス ---
public class ConfigManager : MonoBehaviour
{
    public static ConfigManager Instance { get; private set; }

    /// <summary>設定の初回ロードが完了した際に呼ばれる</summary>
    public event Action OnConfigLoaded;
    
    /// <summary>キャラクターが変更された際に呼ばれる (引数は新しいCharacterId)</summary>
    public event Action<string> OnCharacterChanged;

    public AppSettings Settings { get; private set; }
    private string _savePath;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Windowsの場合、 C:\Users\ユーザー名\AppData\LocalLow\カンパニー名\プロダクト名\settings.json になります
        _savePath = Path.Combine(Application.persistentDataPath, "settings.json");
        LoadSettings();
    }

    public void LoadSettings()
    {
        if (File.Exists(_savePath))
        {
            try
            {
                string json = File.ReadAllText(_savePath);
                Settings = JsonUtility.FromJson<AppSettings>(json);
                Debug.Log("設定をロードしました: " + _savePath);
            }
            catch (Exception e)
            {
                Debug.LogError("設定の読み込みに失敗しました: " + e.Message);
                Settings = new AppSettings();
            }
        }
        else
        {
            Settings = new AppSettings();
        }
        OnConfigLoaded?.Invoke();
    }

    public void SaveSettings()
    {
        try
        {
            string json = JsonUtility.ToJson(Settings, true);
            File.WriteAllText(_savePath, json);
            Debug.Log("設定を保存しました。");
        }
        catch (Exception e)
        {
            Debug.LogError("設定の保存に失敗しました: " + e.Message);
        }
    }

    // --- キャラクター操作用API ---

    /// <summary>キャラクターを登録し即座にSaveする</summary>
    public void AddCharacter(string name, string filePath)
    {
        var newChar = new CharacterData
        {
            id = Guid.NewGuid().ToString(),
            name = name,
            filePath = filePath
        };
        Settings.savedCharacters.Add(newChar);
        SaveSettings();
    }

    /// <summary>
    /// currentCharacterIdを変更してSaveし、OnCharacterChangedを発火する。
    /// 同じIDが渡された場合は何もしない。
    /// </summary>
    public void SetCurrentCharacter(string characterId)
    {
        if (Settings.currentCharacterId == characterId) return;

        Settings.currentCharacterId = characterId;
        SaveSettings();

        OnCharacterChanged?.Invoke(characterId);
    }

    // --- パッケージ操作用API (§4) ---

    /// <summary>パッケージを登録してSaveする</summary>
    public void AddPackage(PackageInfo info)
    {
        Settings.installedPackages.Add(info);
        SaveSettings();
    }

    /// <summary>パッケージを削除してSaveする</summary>
    public void RemovePackage(string packageId)
    {
        Settings.installedPackages.RemoveAll(p => p.id == packageId);
        SaveSettings();
    }
}