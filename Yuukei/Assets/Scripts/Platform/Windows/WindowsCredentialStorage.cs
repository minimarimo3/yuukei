#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

/// <summary>
/// Windows Credential Manager (CRED_TYPE_GENERIC) を使用する実装。
/// advapi32.dll の CredWriteW / CredReadW / CredDeleteW を使用する。
/// </summary>
public class WindowsCredentialStorage : ICredentialStorage
{
    private const int CRED_TYPE_GENERIC = 1;
    private const int CRED_PERSIST_LOCAL_MACHINE = 2;
    private const string TARGET_PREFIX = "Yuukei_";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWriteW(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);

    private static string GetTargetName(string key) => TARGET_PREFIX + key;

    public void Save(string key, string value)
    {
        byte[] byteArray = Encoding.Unicode.GetBytes(value);
        IntPtr blob = IntPtr.Zero;
        try
        {
            blob = Marshal.AllocHGlobal(byteArray.Length);
            Marshal.Copy(byteArray, 0, blob, byteArray.Length);

            var cred = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = GetTargetName(key),
                CredentialBlobSize = (uint)byteArray.Length,
                CredentialBlob = blob,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                UserName = key
            };

            if (!CredWriteW(ref cred, 0))
            {
                int error = Marshal.GetLastWin32Error();
                Debug.LogWarning($"[WindowsCredentialStorage] CredWriteW failed for key '{key}'. Win32 error: {error}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WindowsCredentialStorage] Save failed for key '{key}': {e.Message}");
        }
        finally
        {
            if (blob != IntPtr.Zero)
                Marshal.FreeHGlobal(blob);
        }
    }

    public string Load(string key)
    {
        IntPtr credPtr = IntPtr.Zero;
        try
        {
            if (!CredReadW(GetTargetName(key), CRED_TYPE_GENERIC, 0, out credPtr))
            {
                // キーが存在しない場合は正常系としてnullを返す
                return null;
            }

            var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            if (cred.CredentialBlob == IntPtr.Zero || cred.CredentialBlobSize == 0)
                return null;

            byte[] byteArray = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, byteArray, 0, byteArray.Length);
            return Encoding.Unicode.GetString(byteArray);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WindowsCredentialStorage] Load failed for key '{key}': {e.Message}");
            return null;
        }
        finally
        {
            if (credPtr != IntPtr.Zero)
                CredFree(credPtr);
        }
    }

    public void Delete(string key)
    {
        try
        {
            // 存在しない場合でもエラーにしない
            CredDeleteW(GetTargetName(key), CRED_TYPE_GENERIC, 0);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WindowsCredentialStorage] Delete failed for key '{key}': {e.Message}");
        }
    }
}
#endif
