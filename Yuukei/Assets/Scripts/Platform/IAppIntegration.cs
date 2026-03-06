using System;

/// <summary>
/// OS固有のアプリ統合機能を抽象化するインターフェース。
/// システムトレイ・タスクバー制御・マルチモニター対応などを提供する。
/// 実装クラスは PlatformServiceFactory で生成する。
/// </summary>
public interface IAppIntegration : IDisposable
{
    /// <summary>
    /// システムトレイアイコンを初期化する。
    /// 対応しないOSでは何もしない。
    /// </summary>
    void InitializeTray(Action onSettingsRequested, Action onQuitRequested);

    /// <summary>
    /// タスクバーおよびAlt+Tabからウィンドウを非表示にする。
    /// UniWindowControllerで代替できない場合のみWin32 APIを使う。
    /// </summary>
    void HideFromTaskbar();

    /// <summary>
    /// 仮想スクリーン全体にウィンドウを広げる（マルチモニター対応）。
    /// </summary>
    void SetupMultiMonitor();

    /// <summary>
    /// メインスレッドからUpdate()内で呼ぶ。
    /// STAスレッドからのコールバックをメインスレッドで処理するためのポンプ。
    /// </summary>
    void Tick();
}
