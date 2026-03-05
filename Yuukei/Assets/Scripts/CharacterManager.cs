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
    [SerializeField] private SettingsUIManager settingsUIManager;
    
    [Header("Settings")]
    [SerializeField, Tooltip("ロードしたVRMを配置する親オブジェクト")] 
    private Transform characterRoot;

    // 現在ロードされているVRMのインスタンス
    private Vrm10Instance _currentVrmInstance;

    /// <summary>現在ロードされているVRMインスタンスへの外部参照</summary>
    public Vrm10Instance CurrentVrmInstance => _currentVrmInstance;

    // URPの「Layer 1」を使用する場合は「2」(1 << 1)、「Layer 2」なら「4」(1 << 2)を指定します。
    private uint outlineRenderingLayerMask = 256;

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

        // キャラクターが一つも登録されていない場合、設定画面を開いて追加を促す
        if (configManager.Settings.savedCharacters == null 
            || configManager.Settings.savedCharacters.Count == 0)
        {
            Debug.Log("[CharacterManager] キャラクターが未登録です。設定画面を開きます。");
            if (settingsUIManager != null)
            {
                settingsUIManager.ShowCharacterTab();
            }
            return;
        }

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

                // VRMの全Rendererに対してアウトライン用のRendering Layerを付与する
                ApplyOutlineRenderingLayer(_currentVrmInstance.gameObject);

                // デフォルトのscaleだと小さいので適当に大きくする(2.3)
                _currentVrmInstance.transform.localScale = new Vector3(2.3f, 2.3f, 2.3f);

                Debug.Log($"[CharacterManager] VRMのロードが完了しました: {characterData.name}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CharacterManager] VRMのロードに失敗しました: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // モデル内の全RendererにRendering Layerを追加するメソッド
    private void ApplyOutlineRenderingLayer(GameObject vrmRoot)
    {
        // VRM内のすべてのRenderer (MeshRenderer, SkinnedMeshRendererなど) を取得
        Renderer[] renderers = vrmRoot.GetComponentsInChildren<Renderer>(true);
        
        foreach (Renderer renderer in renderers)
        {
            // 既存のRendering Layer (通常はDefaultの1) に、アウトライン用のレイヤーをOR演算で追加する
            renderer.renderingLayerMask |= outlineRenderingLayerMask;
        }
        
        Debug.Log("[CharacterManager] キャラクターにアウトライン用の Rendering Layer を適用しました。");
    }
}