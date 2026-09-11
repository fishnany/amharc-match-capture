namespace AmharcAgent.Infrastructure.Audio;

using System.Runtime.InteropServices;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;

public sealed class WindowsCredentialManagerAudioCredentialProvider : IAudioCredentialProvider
{
    public const string CredentialTargetName = "AMHARC/Audio/192.168.1.136";

    private readonly IWindowsCredentialReader _reader;

    public WindowsCredentialManagerAudioCredentialProvider()
        : this(new WindowsCredentialReader())
    {
    }

    internal WindowsCredentialManagerAudioCredentialProvider(IWindowsCredentialReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public ValueTask<AudioCredentialResolution> ResolveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = _reader.ReadGeneric(CredentialTargetName);

        if (result.Status == WindowsCredentialReadStatus.NotFound)
        {
            return ValueTask.FromResult(AudioCredentialResolution.Missing());
        }

        if (result.Status != WindowsCredentialReadStatus.Success ||
            string.IsNullOrWhiteSpace(result.Username) ||
            result.Password is null)
        {
            return ValueTask.FromResult(AudioCredentialResolution.Unavailable());
        }

        return ValueTask.FromResult(
            AudioCredentialResolution.Available(
                new AudioCredential(result.Username, result.Password)));
    }
}

internal enum WindowsCredentialReadStatus
{
    Success = 0,
    NotFound = 1,
    Error = 2
}

internal sealed record WindowsCredentialReadResult(
    WindowsCredentialReadStatus Status,
    string? Username,
    string? Password)
{
    public static WindowsCredentialReadResult Success(string username, string password) =>
        new(WindowsCredentialReadStatus.Success, username, password);

    public static WindowsCredentialReadResult NotFound() =>
        new(WindowsCredentialReadStatus.NotFound, null, null);

    public static WindowsCredentialReadResult Error() =>
        new(WindowsCredentialReadStatus.Error, null, null);
}

internal interface IWindowsCredentialReader
{
    WindowsCredentialReadResult ReadGeneric(string targetName);
}

internal sealed class WindowsCredentialReader : IWindowsCredentialReader
{
    private const uint CredTypeGeneric = 1;
    private const int ErrorNotFound = 1168;

    public WindowsCredentialReadResult ReadGeneric(string targetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);

        if (!CredRead(targetName, CredTypeGeneric, 0, out var credentialPointer))
        {
            return Marshal.GetLastWin32Error() == ErrorNotFound
                ? WindowsCredentialReadResult.NotFound()
                : WindowsCredentialReadResult.Error();
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            var username = Marshal.PtrToStringUni(credential.UserName);

            if (string.IsNullOrWhiteSpace(username))
            {
                return WindowsCredentialReadResult.Error();
            }

            var password =
                credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0
                    ? string.Empty
                    : Marshal.PtrToStringUni(
                        credential.CredentialBlob,
                        checked((int)credential.CredentialBlobSize / 2)) ?? string.Empty;

            return WindowsCredentialReadResult.Success(username, password);
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    [DllImport(
        "advapi32.dll",
        EntryPoint = "CredReadW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern bool CredRead(
        string target,
        uint type,
        uint flags,
        out IntPtr credentialPointer);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
