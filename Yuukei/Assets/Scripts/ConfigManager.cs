using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// --- データ構造の定義 ---
[Serializable]
public class CharacterData
{
    public string id;
    public string name;
    public string filePath; // モデルファイルやアセットバンドルの絶対パス
}

[Serializable]
public class AppSettings
{
    public string currentCharacterId = "";
    public List<CharacterData> savedCharacters = new List<CharacterData>();
}

// --- 管理クラス ---
public class ConfigManager : MonoBehaviour
{
    public static ConfigManager Instance { get; private set; }

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

    public void SetCurrentCharacter(string id)
    {
        Settings.currentCharacterId = id;
        SaveSettings();
        // TODO: ここで実際のキャラクターモデルをロード・切り替えるイベントを発火する
        Debug.Log($"キャラクターを切り替えました。ID: {id}");
    }
}