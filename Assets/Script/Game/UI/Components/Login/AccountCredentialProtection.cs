using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

internal static class AccountCredentialProtection
{
    internal static string Protect(string credential) => Convert.ToBase64String(Transform(Encoding.UTF8.GetBytes(credential), true));
    internal static string Unprotect(string protectedCredential) => Encoding.UTF8.GetString(Transform(Convert.FromBase64String(protectedCredential), false));

    static byte[] Transform(byte[] bytes, bool protect)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        var input = new DataBlob { length = bytes.Length, data = Marshal.AllocHGlobal(bytes.Length) };
        var output = new DataBlob();
        try
        {
            Marshal.Copy(bytes, 0, input.data, bytes.Length);
            // Bind remembered sessions to the current Windows user; never fall back to plaintext storage.
            bool success = protect
                ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!success) throw new Win32Exception(Marshal.GetLastWin32Error());
            var result = new byte[output.length];
            Marshal.Copy(output.data, result, 0, result.Length);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(input.data);
            if (output.data != IntPtr.Zero) LocalFree(output.data);
            Array.Clear(bytes, 0, bytes.Length);
        }
#else
        throw new PlatformNotSupportedException("当前平台尚未接入安全凭据存储，请取消记住登录后使用账号密码登录。");
#endif
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    [StructLayout(LayoutKind.Sequential)]
    struct DataBlob { internal int length; internal IntPtr data; }
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CryptProtectData(ref DataBlob input, string description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);
    [DllImport("kernel32.dll")]
    static extern IntPtr LocalFree(IntPtr memory);
#endif
}
