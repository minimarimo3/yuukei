using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// プラットフォーム抽象化レイヤーのシーン上エントリーポイント。
/// IAppIntegration を生成し、ライフサイクルを管理しながら、アプリの起動シーケンスを統括する。
/// </summary>
public class PlatformBootstrapper : MonoBehaviour
{
    [SerializeField] private SettingsUIManager settingsUI;
    [SerializeField] private CharacterManager characterManager;
    [SerializeField] private ObjectToBottomRight objectToBottomRight;

    private IAppIntegration _appIntegration;

    private void Awake()
    {
        _appIntegration = PlatformServiceFactory.CreateAppIntegration();
    }

    private void Start()
    {
        BootSequenceAsync().Forget();
    }

    private async UniTaskVoid BootSequenceAsync()
    {

        Debug.Log("[BootSequence] 0. 描画の一時停止");
    
        Camera mainCam = Camera.main;
        int originalCullingMask = 0;
    
        // 3Dオブジェクトの描画を停止（メッシュ自体は存在するためBounds計算は可能）
        if (mainCam != null)
        {
            originalCullingMask = mainCam.cullingMask;
            mainCam.cullingMask = 0; 
        }

        // UIの描画を停止（Canvasコンポーネントのみを無効化し、RectTransform等の計算は維持）
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            c.enabled = false;
        }

        Debug.Log("[BootSequence] 1. 設定のロード待機 (ConfigManager.Awakeで完了済)");

        Debug.Log("[BootSequence] 2. パッケージとテーマ・プラグインの復元");
        if (PackageManager.Instance != null)
        {
            await PackageManager.Instance.RestorePackagesAsync();
        }

        Debug.Log("[BootSequence] 3. キャラクターのロード完了待機");
        if (characterManager == null)
            characterManager = FindObjectOfType<CharacterManager>();
            
        if (characterManager != null)
        {
            await characterManager.RestoreCharacterAsync();
        }

        // Script Runner (DaihonScenarioManager) の初期化を確実に行う
        if (DaihonScenarioManager.Instance == null)
        {
            var go = new GameObject("DaihonScenarioManager");
            go.AddComponent<DaihonScenarioManager>();
        }
        DaihonScenarioManager.Instance.LoadLocalScripts();

        // メッシュとBoundsの更新を確実に待つ
        await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);

        Debug.Log("[BootSequence] 4. ウィンドウ座標の再計算 (キャラクターメッシュ基準)");
        if (objectToBottomRight == null)
            objectToBottomRight = FindObjectOfType<ObjectToBottomRight>();

        var positioners = FindObjectsOfType<ObjectToBottomRight>();
        foreach (var p in positioners)
        {
            p.PositionAtBottomRight();
        }

        Debug.Log("[BootSequence] 5. OS連携機能の初期化 (タスクトレイ・マルチモニター・タスクバー非表示)");
        if (_appIntegration != null)
        {
            // システムトレイアイコンを初期化
            _appIntegration.InitializeTray(
                onSettingsRequested: () =>
                {
                    if (settingsUI != null)
                        settingsUI.ShowSettings();
                    else
                        Debug.LogWarning("[PlatformBootstrapper] SettingsUIManager の参照が設定されていません。");
                },
                onQuitRequested: Application.Quit
            );

            // マルチモニター対応
            _appIntegration.SetupMultiMonitor();

            // UniWindowController の初期化完了を待ってからタスクバーを非表示にする
            await UniTask.WaitUntil(() => Time.frameCount > 5);
            await UniTask.Delay(100);
            
            _appIntegration.HideFromTaskbar();
        }

        Debug.Log("[BootSequence] 6. 描画の再開");
    
        // 舞台裏のセットアップが完全に終わったので、幕を上げる
        if (mainCam != null)
        {
            mainCam.cullingMask = originalCullingMask;
        }
        foreach (var c in canvases)
        {
            c.enabled = true;
        }

        Debug.Log("[BootSequence] 6. トリガーの監視開始");
        if (TriggerManager.Instance != null)
        {
            TriggerManager.Instance.StartMonitoring();
        }
        
        Debug.Log("[BootSequence] ブートシーケンス完了");
    }


    private void Update()
    {
        _appIntegration?.Tick();
    }

    private void OnDestroy()
    {
        _appIntegration?.Dispose();
        _appIntegration = null;
    }
}
