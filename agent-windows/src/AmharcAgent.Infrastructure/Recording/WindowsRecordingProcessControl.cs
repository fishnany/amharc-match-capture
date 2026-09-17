using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace AmharcAgent.Infrastructure.Recording;

/// <summary>
/// Owns the Windows-specific FFmpeg process-group boundary used by Recording.
/// A dedicated process group allows Stop to send CTRL_BREAK to Recording FFmpeg
/// without signalling the Capture Agent itself.
/// </summary>
public interface IRecordingProcessControl
{
    RecordingProcessLaunch Start(
        string executablePath,
        string arguments,
        EventHandler exitedHandler);

    void RequestGracefulTermination(int processGroupId);
}

public sealed record RecordingProcessLaunch(
    Process Process,
    Stream StandardInput,
    StreamReader StandardError);

public sealed class WindowsRecordingProcessControl : IRecordingProcessControl
{
    private const uint CreateNewProcessGroup = 0x00000200;
    private const uint CtrlBreakEvent = 1;
    private const uint StartfUseStdHandles = 0x00000100;
    private const uint HandleFlagInherit = 0x00000001;

    public RecordingProcessLaunch Start(
        string executablePath,
        string arguments,
        EventHandler exitedHandler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(exitedHandler);

        var stdin = new AnonymousPipeServerStream(
            PipeDirection.Out,
            HandleInheritability.Inheritable);
        var stderr = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);

        try
        {
            ClearServerHandleInheritance(stdin);
            ClearServerHandleInheritance(stderr);

            var startupInfo = new StartupInfo
            {
                cb = Marshal.SizeOf<StartupInfo>(),
                dwFlags = StartfUseStdHandles,
                hStdInput = ParseHandle(stdin.GetClientHandleAsString()),
                hStdOutput = ParseHandle(stderr.GetClientHandleAsString()),
                hStdError = ParseHandle(stderr.GetClientHandleAsString())
            };

            var commandLine =
                $"\"{executablePath}\" {arguments}";
            var currentDirectory =
                Path.GetDirectoryName(executablePath);

            if (string.IsNullOrWhiteSpace(currentDirectory))
            {
                currentDirectory = Environment.CurrentDirectory;
            }

            if (!CreateProcess(
                    executablePath,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    true,
                    CreateNewProcessGroup,
                    IntPtr.Zero,
                    currentDirectory,
                    ref startupInfo,
                    out var processInformation))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                stdin.DisposeLocalCopyOfClientHandle();
                stderr.DisposeLocalCopyOfClientHandle();

                var process =
                    Process.GetProcessById(processInformation.dwProcessId);
                process.EnableRaisingEvents = false;
                process.Exited += exitedHandler;
                process.EnableRaisingEvents = true;

                var stderrReader =
                    new StreamReader(stderr);

                return new RecordingProcessLaunch(
                    process,
                    stdin,
                    stderrReader);
            }
            finally
            {
                CloseHandle(processInformation.hThread);
                CloseHandle(processInformation.hProcess);
            }
        }
        catch
        {
            stdin.Dispose();
            stderr.Dispose();
            throw;
        }
    }

    public void RequestGracefulTermination(
        int processGroupId)
    {
        if (processGroupId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(processGroupId));
        }

        if (!GenerateConsoleCtrlEvent(
                CtrlBreakEvent,
                checked((uint)processGroupId)))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error());
        }
    }

    private static void ClearServerHandleInheritance(
        AnonymousPipeServerStream pipe)
    {
        if (!SetHandleInformation(
                pipe.SafePipeHandle.DangerousGetHandle(),
                HandleFlagInherit,
                0))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error());
        }
    }

    private static IntPtr ParseHandle(string value)
    {
        return new IntPtr(
            long.Parse(
                value,
                System.Globalization.CultureInfo.InvariantCulture));
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public uint dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(
        string lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        ref StartupInfo lpStartupInfo,
        out ProcessInformation lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GenerateConsoleCtrlEvent(
        uint dwCtrlEvent,
        uint dwProcessGroupId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(
        IntPtr hObject,
        uint dwMask,
        uint dwFlags);

}
