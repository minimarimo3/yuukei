// ==========================================================================
// PluginContracts.cs
// Yuukei.Plugin.Contracts で定義するインターフェース（§7.2）。
// プラグイン開発者向けのインターフェース定義。
// 将来的には Yuukei.Plugin.Contracts.dll として配布する。
// ==========================================================================

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Daihon;

// ── トリガー拡張 ──────────────────────────────────────────────────────────

/// <summary>
/// トリガープラグインのインターフェース（§7.2）。
/// triggers.json の "type" フィールドと TriggerName を一致させること。
/// </summary>
public interface ITriggerPlugin : IDisposable
{
    /// <summary>一意な識別子。triggers.json の "type" フィールドと一致させる</summary>
    string TriggerName { get; }

    void Initialize(ITriggerContext context);

    event Action<TriggerPayload> OnFired;
}

/// <summary>トリガープラグインに渡されるコンテキスト（§7.2）。</summary>
public interface ITriggerContext
{
    void ExecuteScript(string scriptAbsolutePath);
    IVariableStore VariableStore { get; }
}

/// <summary>トリガー発火時に渡されるペイロード（§7.2）。</summary>
public class TriggerPayload
{
    public string TriggerName { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
}

// ── 台本DSL関数拡張 ──────────────────────────────────────────────────────

/// <summary>
/// 台本DSLのカスタム関数インターフェース（§7.2）。
/// プラグインが追加する関数はこのインターフェースを実装する。
/// </summary>
public interface IDaihonFunction
{
    string FunctionName { get; }
    UniTask<DaihonValue> CallAsync(
        IReadOnlyList<DaihonValue> positionalArgs,
        IReadOnlyDictionary<string, DaihonValue> namedArgs);
}
