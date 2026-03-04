using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UniVRM10;

public class CharacterManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ConfigManager configManager;
    
    [Header("Settings")]
    [SerializeField, Tooltip("ロードしたVRMを配置する親オブジェクト")] 
    private Transform characterRoot;

    // 現在ロードされているVRMのインスタンス
    private Vrm10Instance _currentVrmInstance;

    private void Start()
    {
        if (configManager != null)
        {
            // イベントの購読
            configManager.OnConfigLoaded += HandleConfigLoaded;
            configManager.OnCharacterChanged += HandleCharacterChanged;

            // ConfigManager.Awake() で OnConfigLoaded は既に発火済みのため、
            // 初回のキャラクターロードを手動で実行する
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
            // 購読解除 (メモリリーク防止)
            configManager.OnConfigLoaded -= HandleConfigLoaded;
            configManager.OnCharacterChanged -= HandleCharacterChanged;
        }
    }

    private void HandleConfigLoaded()
    {
        if (configManager?.Settings == null) return;
        var currentId = configManager.Settings.currentCharacterId;
        LoadCharacterAsync(currentId).ConfigureAwait(false);
    }

    private void HandleCharacterChanged(string newCharacterId)
    {
        LoadCharacterAsync(newCharacterId).ConfigureAwait(false);
    }

    /// <summary>
    /// 非同期でVRMをロードする
    /// </summary>
    public async Task LoadCharacterAsync(string characterId)
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
            Debug.LogError($"[CharacterManager] VRMファイルが存在しません: {path}");
            return;
        }

        // 既存のモデルが存在する場合は安全に破棄する
        if (_currentVrmInstance != null)
        {
            Destroy(_currentVrmInstance.gameObject);
            _currentVrmInstance = null;
            // 補足: ガベージコレクションを促したい場合は Resources.UnloadUnusedAssets() を呼ぶことも検討
        }

        try
        {
            Debug.Log($"[CharacterManager] VRMのロードを開始します: {characterData.name}");

            // UniVRM10 による非同期ロード
            // canLoadVrm0X=true にすることで、VRM 0.x系と1.0系の両方に対応可能
            _currentVrmInstance = await Vrm10.LoadPathAsync(
                path,
                canLoadVrm0X: true,
                showMeshes: true
            );

            if (_currentVrmInstance != null)
            {
                // 親オブジェクトが指定されている場合は、その下に配置する
                if (characterRoot != null)
                {
                    _currentVrmInstance.transform.SetParent(characterRoot, false);
                }

                // ロード直後にやりたい初期化処理があればここに記述します。
                // (例: アニメーターの設定、カメラの注視点(LookAt)の設定など)

                Debug.Log($"[CharacterManager] VRMのロードが完了しました: {characterData.name}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CharacterManager] VRMのロードに失敗しました: {ex.Message}\n{ex.StackTrace}");
        }
    }
}