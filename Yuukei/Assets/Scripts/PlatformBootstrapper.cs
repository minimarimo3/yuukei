using System.Collections;
using UnityEngine;

/// <summary>
/// プラットフォーム抽象化レイヤーのシーン上エントリーポイント。
/// IAppIntegration を生成し、ライフサイクルを管理する。
/// 旧 SystemTrayManager / MultiMonitorEnabler / TaskbarIconHider を置き換える。
/// </summary>
public class PlatformBootstrapper : MonoBehaviour
{
    [SerializeField] private SettingsUIManager settingsUI;

    private IAppIntegration _appIntegration;

    private void Awake()
    {
        _appIntegration = PlatformServiceFactory.CreateAppIntegration();
    }

    private IEnumerator Start()
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
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        _appIntegration.HideFromTaskbar();
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
