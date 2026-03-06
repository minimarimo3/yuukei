using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 非Windows環境向けのフォールバック実装。
/// APIキーをメモリ上の Dictionary にのみ保持し、永続化しない。
/// TODO: macOS は Keychain、Android は Keystore を使った実装に置き換える。
/// </summary>
public class DummyCredentialStorage : ICredentialStorage
{
    private readonly Dictionary<string, string> _store = new Dictionary<string, string>();

    public void Save(string key, string value)
    {
        _store[key] = value;
        Debug.LogWarning("[DummyCredentialStorage] Credentials stored in-memory only. They will not persist across sessions.");
    }

    public string Load(string key)
    {
        _store.TryGetValue(key, out var value);
        return value;
    }

    public void Delete(string key)
    {
        _store.Remove(key);
    }
}
