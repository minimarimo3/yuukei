// ==========================================================================
// SystemEventTrigger.cs
// CPU使用率・バッテリー残量の閾値超過で発火するトリガー（§8.2）。
// ==========================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CPU使用率・バッテリー残量の閾値超過で発火するトリガー（§8.2）。
/// params: cpuThreshold(float), batteryThreshold(float)
/// </summary>
public class SystemEventTrigger : ITriggerPlugin
{
    public string TriggerName => "SystemEventTrigger";

    public event Action<TriggerPayload> OnFired;

    private float _cpuThreshold = 80f;
    private float _batteryThreshold = 20f;
    private ITriggerContext _context;

    private float _checkInterval = 30f;
    private float _timeSinceLastCheck = 0f;

    public void Initialize(ITriggerContext context)
    {
        _context = context;
    }

    public void SetParams(float cpuThreshold, float batteryThreshold)
    {
        _cpuThreshold = cpuThreshold;
        _batteryThreshold = batteryThreshold;
    }

    /// <summary>定期的に呼ばれるチェック。TriggerManager.Update から呼ぶ。</summary>
    public void Tick(float deltaTime)
    {
        _timeSinceLastCheck += deltaTime;
        if (_timeSinceLastCheck < _checkInterval) return;
        _timeSinceLastCheck = 0f;

        float battery = SystemInfo.batteryLevel;
        // batteryLevel は 0〜1 の範囲（不明時は -1）
        if (battery >= 0 && battery * 100f < _batteryThreshold)
        {
            OnFired?.Invoke(new TriggerPayload
            {
                TriggerName = TriggerName,
                Parameters = new Dictionary<string, object>
                {
                    { "type", "battery" },
                    { "batteryLevel", battery * 100f }
                }
            });
        }
    }

    public void Dispose() { }
}
