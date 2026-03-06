// ==========================================================================
// ExplorerItemTrigger.cs
// Explorerアイテム座標とキャラ位置の重なりで発火するトリガー（§8.2）。
// UNITY_STANDALONE_WIN のみ実装する（§14.4参照）。
// ==========================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Explorerアイテム座標とキャラ位置の重なりで発火するトリガー（§8.2）。
/// params: targetName(string)
/// ExplorerItemScanner と連携してキャラクターとの重なりを検出する。
/// </summary>
public class ExplorerItemTrigger : ITriggerPlugin
{
    public string TriggerName => "ExplorerItemTrigger";

    public event Action<TriggerPayload> OnFired;

    private string _targetName;
    private ITriggerContext _context;
    private bool _wasOverlapping;

    public void Initialize(ITriggerContext context)
    {
        _context = context;
    }

    public void SetParams(string targetName)
    {
        _targetName = targetName;
    }

    /// <summary>
    /// ExplorerItemScanner から得た矩形とキャラクター位置を比較して発火する。
    /// TriggerManager から呼ばれる。
    /// </summary>
    public void CheckOverlap(Rect itemRect, Vector2 characterScreenPos)
    {
        bool isOverlapping = itemRect.Contains(characterScreenPos);

        if (isOverlapping && !_wasOverlapping)
        {
            _wasOverlapping = true;
            OnFired?.Invoke(new TriggerPayload
            {
                TriggerName = TriggerName,
                Parameters = new Dictionary<string, object>
                {
                    { "targetName", _targetName }
                }
            });
        }
        else if (!isOverlapping)
        {
            _wasOverlapping = false;
        }
    }

    public void Dispose() { }
}
