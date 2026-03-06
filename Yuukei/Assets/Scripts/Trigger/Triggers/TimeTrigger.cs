// ==========================================================================
// TimeTrigger.cs
// 指定時刻に発火するトリガー（§8.2）。
// ==========================================================================

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 指定した時刻（hour, minute）かつ指定曜日に発火するトリガー（§8.2）。
/// params: hour(int), minute(int), days(string[])
/// </summary>
public class TimeTrigger : ITriggerPlugin
{
    public string TriggerName => "TimeTrigger";

    public event Action<TriggerPayload> OnFired;

    private int _hour;
    private int _minute;
    private HashSet<string> _days;
    private ITriggerContext _context;
    private bool _firedToday;
    private int _lastCheckedDay = -1;

    public void Initialize(ITriggerContext context)
    {
        _context = context;
    }

    /// <summary>params オブジェクトから設定を読み込む。</summary>
    public void SetParams(int hour, int minute, string[] days)
    {
        _hour = hour;
        _minute = minute;
        _days = new HashSet<string>(days ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
    }

    private static readonly string[] DayNames = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

    /// <summary>毎フレーム呼ばれる時刻チェック。TriggerManager.Update から呼ぶ。</summary>
    public void Tick()
    {
        var now = DateTime.Now;

        // 日付が変わったらリセット
        if (_lastCheckedDay != now.Day)
        {
            _lastCheckedDay = now.Day;
            _firedToday = false;
        }

        if (_firedToday) return;

        if (now.Hour != _hour || now.Minute != _minute) return;

        // 曜日チェック（days が空の場合は毎日）
        if (_days.Count > 0)
        {
            string dayStr = DayNames[(int)now.DayOfWeek];
            if (!_days.Contains(dayStr)) return;
        }

        _firedToday = true;
        OnFired?.Invoke(new TriggerPayload
        {
            TriggerName = TriggerName,
            Parameters = new Dictionary<string, object>
            {
                { "hour", _hour },
                { "minute", _minute }
            }
        });
    }

    public void Dispose() { }
}
