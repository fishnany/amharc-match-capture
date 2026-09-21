namespace AmharcAgent.Infrastructure.Security;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;

public sealed class WindowsCredentialManagerStore : IProtectedCredentialStore
{
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    public ValueTask<ProtectedCredential?> ReadAsync(
        string targetName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);

        if (!CredRead(targetName, CredTypeGeneric, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
                return ValueTask.FromResult<ProtectedCredential?>(null);

            throw new Win32Exception(error, "Windows Credential Manager read failed.");
        }

        try
        {
            var native = Marshal.PtrToStructure<NativeCredential>(pointer);
            var username = native.UserName ?? string.Empty;
            var secret = native.CredentialBlob == IntPtr.Zero || native.CredentialBlobSize == 0
                ? string.Empty
                : Marshal.PtrToStringUni(
                    native.CredentialBlob,
                    checked((int)native.CredentialBlobSize / 2)) ?? string.Empty;

            return ValueTask.FromResult<ProtectedCredential?>(
                new ProtectedCredential(username, secret));
        }
        finally
        {
            CredFree(pointer);
        }
    }

    public ValueTask WriteAsync(
        string targetName,
        ProtectedCredential credential,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
        ArgumentNullException.ThrowIfNull(credential);

        var secretBytes = Encoding.Unicode.GetBytes(credential.Secret);
        var blob = IntPtr.Zero;

        try
        {
            if (secretBytes.Length > 0)
            {
                blob = Marshal.AllocCoTaskMem(secretBytes.Length);
                Marshal.Copy(secretBytes, 0, blob, secretBytes.Length);
            }

            var native = new NativeCredential
            {
                Type = CredTypeGeneric,
                TargetName = targetName,
                CredentialBlobSize = checked((uint)secretBytes.Length),
                CredentialBlob = blob,
                Persist = CredPersistLocalMachine,
                UserName = credential.Username
            };

            if (!CredWrite(ref native, 0))
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Windows Credential Manager write failed.");
        }
        finally
        {
            if (blob != IntPtr.Zero)
                Marshal.FreeCoTaskMem(blob);

            Array.Clear(secretBytes, 0, secretBytes.Length);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(
        string targetName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);

        if (!CredDelete(targetName, CredTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
                throw new Win32Exception(
                    error,
                    "Windows Credential Manager delete failed.");
        }

        return ValueTask.CompletedTask;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credentialPointer);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref NativeCredential userCredential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string? TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }
}
