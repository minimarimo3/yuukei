// ==========================================================================
// AppIntegrationManager.cs
// IAppIntegration を保持し、Unityのライフサイクルに橋渡しするMonoBehaviour（§14.1）。
// ==========================================================================

using System.Collections;
using UnityEngine;

/// <summary>
/// IAppIntegration を保持し、Unityのライフサイクルに橋渡しするMonoBehaviour（§14.1）。
/// 旧 PlatformBootstrapper の後継。
/// </summary>
public class AppIntegrationManager : MonoBehaviour
{
    [SerializeField] private SettingsUIManager settingsUI;

    private IAppIntegration _integration;

    private void Awake()
    {
        // Application.isEditor のチェックはここで行う（§14.1参照）
        if (Application.isEditor)
        {
            _integration = new DummyAppIntegration();
            return;
        }
        _integration = PlatformServiceFactory.CreateAppIntegration();
    }

    private IEnumerator Start()
    {
        // システムトレイアイコンを初期化（§14.1）
        _integration.InitializeTray(
            onSettingsRequested: () =>
            {
                if (settingsUI != null)
                    settingsUI.ShowSettings();
                else
                    Debug.LogWarning("[AppIntegrationManager] SettingsUIManager の参照が設定されていません。");
            },
            onQuitRequested: Application.Quit
        );

        // マルチモニター対応（§14.3）
        _integration.SetupMultiMonitor();

        // UniWindowController の初期化完了を待ってからタスクバーを非表示にする（§14.2）
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        _integration.HideFromTaskbar();
    }

    private void Update()
    {
        _integration?.Tick();
    }

    private void OnDestroy()
    {
        _integration?.Dispose();
        _integration = null;
    }
}
