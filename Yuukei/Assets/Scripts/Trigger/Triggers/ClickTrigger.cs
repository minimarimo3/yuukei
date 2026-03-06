// ==========================================================================
// ClickTrigger.cs
// キャラクターへの左クリックで発火するトリガー（§8.2）。
// ==========================================================================

using System;
using System.Collections.Generic;

/// <summary>
/// キャラクターへの左クリックで発火するトリガー（§8.2）。
/// TriggerManager が OnCharacterClicked() を呼ぶことで発火する。
/// </summary>
public class ClickTrigger : ITriggerPlugin
{
    public string TriggerName => "ClickTrigger";

    public event Action<TriggerPayload> OnFired;

    private ITriggerContext _context;

    public void Initialize(ITriggerContext context)
    {
        _context = context;
    }

    /// <summary>キャラクタークリック時に TriggerManager から呼ばれる。</summary>
    public void Fire()
    {
        OnFired?.Invoke(new TriggerPayload
        {
            TriggerName = TriggerName,
            Parameters = new Dictionary<string, object>()
        });
    }

    public void Dispose() { }
}
