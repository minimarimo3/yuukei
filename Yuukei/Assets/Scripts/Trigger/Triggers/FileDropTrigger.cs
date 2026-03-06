// ==========================================================================
// FileDropTrigger.cs
// ウィンドウへのファイルドロップで発火するトリガー（§8.2）。
// ==========================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// ウィンドウへのファイルドロップで発火するトリガー（§8.2）。
/// params: extensions(string[])
/// FileDropRouter から OnFilesDropped() を呼ぶことで発火する。
/// </summary>
public class FileDropTrigger : ITriggerPlugin
{
    public string TriggerName => "FileDropTrigger";

    public event Action<TriggerPayload> OnFired;

    private HashSet<string> _extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private ITriggerContext _context;

    public void Initialize(ITriggerContext context)
    {
        _context = context;
    }

    public void SetParams(string[] extensions)
    {
        _extensions = extensions != null
            ? new HashSet<string>(extensions.Select(e => e.ToLower()), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>ファイルドロップ時に呼ばれる。</summary>
    public void OnFilesDropped(string[] filePaths)
    {
        foreach (var path in filePaths)
        {
            var ext = Path.GetExtension(path).ToLower();
            if (_extensions.Count == 0 || _extensions.Contains(ext))
            {
                OnFired?.Invoke(new TriggerPayload
                {
                    TriggerName = TriggerName,
                    Parameters = new Dictionary<string, object>
                    {
                        { "filePath", path },
                        { "extension", ext }
                    }
                });
            }
        }
    }

    public void Dispose() { }
}
