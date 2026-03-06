using System;

/// <summary>
/// 非Windows環境向けの空実装。すべてのメソッドが何もしない。
/// </summary>
public class DummyAppIntegration : IAppIntegration
{
    public void InitializeTray(Action onSettingsRequested, Action onQuitRequested) { }
    public void HideFromTaskbar() { }
    public void SetupMultiMonitor() { }
    public void Tick() { }
    public void Dispose() { }
}
