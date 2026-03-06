using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UniVRM10;

public class CharacterManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ConfigManager configManager;
    [SerializeField] private SettingsUIManager settingsUIManager;
    [SerializeField] private VRMLipSync lipSync;
    
    [Header("Settings")]
    [SerializeField, Tooltip("ロードしたVRMを配置する親オブジェクト")] 
    private Transform characterRoot;

    private Vrm10Instance _currentVrmInstance;
    public Vrm10Instance CurrentVrmInstance => _currentVrmInstance;

    private uint outlineRenderingLayerMask = 256;

    private void Start()
    {
        if (configManager != null)
        {
            configManager.OnConfigLoaded += HandleConfigLoaded;
            configManager.OnCharacterChanged += HandleCharacterChanged;

            if (configManager.Settings != null 
                && !string.IsNullOrEmpty(configManager.Settings.currentCharacterId))
            {
                HandleConfigLoaded();
            }
        }
    }

    private void OnDestroy()
    {
        if (configManager != null)
        {
            configManager.OnConfigLoaded -= HandleConfigLoaded;
            configManager.OnCharacterChanged -= HandleCharacterChanged;
        }
    }

    private void HandleConfigLoaded()
    {
        if (configManager?.Settings == null) return;

        if (configManager.Settings.savedCharacters == null 
            || configManager.Settings.savedCharacters.Count == 0)
        {
            Debug.Log("[CharacterManager] キャラクターが未登録です。設定画面を開きます。");
            if (settingsUIManager != null) settingsUIManager.ShowCharacterTab();
            return;
        }

        var currentId = configManager.Settings.currentCharacterId;
        LoadCharacterAsync(currentId).Forget();
    }

    private void HandleCharacterChanged(string newCharacterId)
    {
        LoadCharacterAsync(newCharacterId).Forget();
    }

    public async UniTask LoadCharacterAsync(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return;
        if (configManager?.Settings?.savedCharacters == null) return;

        var characterData = configManager.Settings.savedCharacters.FirstOrDefault(c => c.id == characterId);
        if (characterData == null)
        {
            Debug.LogWarning($"[CharacterManager] 選択されたキャラクター(ID:{characterId})がリストに見つかりません。");
            return;
        }

        string path = characterData.filePath;
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[CharacterManager] VRMファイルが見つかりません: {path}");
            return;
        }

        // 既存キャラを破棄（§11.3）
        if (_currentVrmInstance != null)
        {
            Destroy(_currentVrmInstance.gameObject);
            _currentVrmInstance = null;
        }

        try
        {
            Debug.Log($"[CharacterManager] VRMのロードを開始します: {characterData.name}");

            _currentVrmInstance = await Vrm10.LoadPathAsync(
                path,
                canLoadVrm0X: true,
                showMeshes: true
            );

            if (_currentVrmInstance != null)
            {
                if (characterRoot != null)
                {
                    _currentVrmInstance.transform.SetParent(characterRoot, false);
                }
                
                ApplyOutlineRenderingLayer(_currentVrmInstance.gameObject);

                // 配置スクリプトを取得して再計算させる（§11.3）
                characterRoot?.GetComponent<ObjectToBottomRight>()?.PositionAtBottomRight();

                // VRMLipSync のキャッシュを無効化する（§11.3）
                if (lipSync != null)
                {
                    lipSync.InvalidateCache();
                }

                Debug.Log($"[CharacterManager] VRMのロードが完了しました: {characterData.name}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CharacterManager] VRMのロードに失敗しました: {ex.Message}\n{ex.StackTrace}");
            _currentVrmInstance = null;
        }
    }

    private void ApplyOutlineRenderingLayer(GameObject vrmRoot)
    {
        Renderer[] renderers = vrmRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            renderer.renderingLayerMask |= outlineRenderingLayerMask;
        }
    }
}