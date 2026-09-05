using System.Diagnostics;
using System.Text.RegularExpressions;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Recording;

/// <summary>
/// Records an RTSP stream to MKV segments using FFmpeg.
/// The recording elapsed-time clock is completely independent of the match clock.
/// </summary>
public class FfmpegRecordingService : IRecordingService, IAsyncDisposable
{
    private readonly ILogger<FfmpegRecordingService> _logger;
    private readonly IRecordingSessionStore _sessionStore;
    private readonly ICameraAdapter _camera;
    private readonly string _ffmpegPath;

    private Process? _ffmpegProcess;
    private Task<string>? _ffmpegStderrTask;
    private readonly Stopwatch _elapsedStopwatch = new();

    private RecordingState _state = RecordingState.Idle;
    private RecordingOptions? _currentOptions;
    private RecordingSession? _currentSession;

    private readonly object _lock = new();

    public event Action<RecordingState>? StateChanged;

    public FfmpegRecordingService(
        ILogger<FfmpegRecordingService> logger,
        IRecordingSessionStore sessionStore,
        ICameraAdapter camera,
        string ffmpegPath = "ffmpeg.exe")
    {
        _logger = logger;
        _sessionStore = sessionStore;
        _camera = camera;
        _ffmpegPath = ffmpegPath;
    }

    public RecordingState State => _state;

    public double ElapsedSeconds =>
        _elapsedStopwatch.Elapsed.TotalSeconds;

    public int SegmentCount =>
        GetSegments().Count;

    public string? OutputDirectory =>
        _currentOptions?.OutputDirectory;

    public async Task StartRecordingAsync(
        RecordingOptions options,
        CancellationToken ct = default)
    {
        if (_state == RecordingState.Recording)
        {
            throw new InvalidOperationException(
                "Already recording.");
        }

        SetState(RecordingState.Starting);

        _currentOptions = options;

        _currentSession = new RecordingSession
        {
            RecordingId = Guid.NewGuid().ToString(),
            MatchId = options.MatchId,
            CameraId = options.CameraId,
            State = RecordingState.Starting,

            // Persist only the non-secret RTSP endpoint.
            // Authentication is resolved at runtime.
            RtspUrl = options.RtspUrl,

            OutputDirectory = options.OutputDirectory,
            StartedAt = DateTimeOffset.UtcNow,
            SegmentDurationSeconds =
                options.SegmentDurationSeconds,
            IncludeAudio = options.IncludeAudio,
            SegmentCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _sessionStore.SaveAsync(
            _currentSession,
            ct);

        try
        {
            Directory.CreateDirectory(
                options.OutputDirectory);

            var outputPattern = Path.Combine(
                options.OutputDirectory,
                $"{options.MatchId}_%03d.mkv");

            if (_camera.ConnectionState !=
                CameraConnectionState.Connected)
            {
                await _camera.ConnectAsync(ct);
            }

            // Runtime-only authenticated endpoint.
            // This value must not be persisted or logged.
            var runtimeRtspUrl =
                await _camera.GetAuthenticatedStreamUrlAsync(
                    null,
                    ct);

            var mapArgs =
                options.IncludeAudio
                    ? "-map 0:v:0 -map 0:a?"
                    : "-map 0:v:0";

            var audioArgs =
                options.IncludeAudio
                    ? "-c:a copy"
                    : "-an";

            var args = string.Join(
                " ",
                "-rtsp_transport tcp",
                $"-i \"{runtimeRtspUrl}\"",
                mapArgs,
                "-c:v copy",
                "-bsf:v \"setts=pts='if(eq(PTS,NOPTS),N*3600,PTS)':dts='if(eq(DTS,NOPTS),(N-1)*3600,DTS)'\"",
                "-avoid_negative_ts make_zero",
                audioArgs,
                "-f segment",
                $"-segment_time {options.SegmentDurationSeconds}",
                "-segment_format mkv",
                "-reset_timestamps 1",
                "-segment_start_number 0",
                $"\"{outputPattern}\"");

            // Do not log 'args': it contains the authenticated runtime RTSP URI.
            _logger.LogInformation(
                "Starting FFmpeg recording for match {MatchId} using camera {CameraId}; output directory={OutputDirectory}, segment duration={SegmentDurationSeconds}s",
                options.MatchId,
                options.CameraId,
                options.OutputDirectory,
                options.SegmentDurationSeconds);

            _ffmpegProcess = CreateFfmpegProcess(args);

            _ffmpegProcess.Start();

            // Capture the complete stderr stream so that short-lived FFmpeg
            // failures cannot disappear between asynchronous line callbacks.
            _ffmpegStderrTask =
                _ffmpegProcess.StandardError.ReadToEndAsync();

            _ffmpegProcess.EnableRaisingEvents = true;

            _elapsedStopwatch.Restart();

            SetState(RecordingState.Recording);

            _currentSession.State =
                RecordingState.Recording;

            _currentSession.SegmentCount =
                GetSegments().Count;

            _currentSession.UpdatedAt =
                DateTimeOffset.UtcNow;

            await _sessionStore.SaveAsync(
                _currentSession,
                ct);

            _logger.LogInformation(
                "Recording started for match {MatchId}; recording session {RecordingId} persisted",
                options.MatchId,
                _currentSession.RecordingId);
        }
        catch
        {
            _elapsedStopwatch.Stop();

            SetState(RecordingState.Error);

            _currentSession.State =
                RecordingState.Error;

            _currentSession.SegmentCount =
                GetSegments().Count;

            _currentSession.UpdatedAt =
                DateTimeOffset.UtcNow;

            await _sessionStore.SaveAsync(
                _currentSession,
                CancellationToken.None);

            throw;
        }
    }

    public async Task StopRecordingAsync(
        CancellationToken ct = default)
    {
        if (_ffmpegProcess is null ||
            _state != RecordingState.Recording)
        {
            return;
        }

        SetState(RecordingState.Stopping);

        if (_currentSession is not null)
        {
            _currentSession.State =
                RecordingState.Stopping;

            _currentSession.SegmentCount =
                GetSegments().Count;

            _currentSession.UpdatedAt =
                DateTimeOffset.UtcNow;

            await _sessionStore.SaveAsync(
                _currentSession,
                ct);
        }

        try
        {
            // Send 'q' to FFmpeg stdin for a clean segment-boundary shutdown.
            await _ffmpegProcess.StandardInput
                .WriteAsync('q');

            await _ffmpegProcess.StandardInput
                .FlushAsync(ct);

            if (!_ffmpegProcess.WaitForExit(10_000))
            {
                _logger.LogWarning(
                    "FFmpeg did not exit cleanly — killing process");

                _ffmpegProcess.Kill();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error stopping FFmpeg");
        }
        finally
        {
            _elapsedStopwatch.Stop();

            _ffmpegProcess = null;
            _ffmpegStderrTask = null;

            SetState(RecordingState.Complete);

            if (_currentSession is not null)
            {
                _currentSession.State =
                    RecordingState.Complete;

                _currentSession.StoppedAt =
                    DateTimeOffset.UtcNow;

                _currentSession.SegmentCount =
                    GetSegments().Count;

                _currentSession.UpdatedAt =
                    DateTimeOffset.UtcNow;

                await _sessionStore.SaveAsync(
                    _currentSession,
                    CancellationToken.None);
            }

            _logger.LogInformation(
                "Recording stopped. Elapsed: {Seconds:F1}s",
                ElapsedSeconds);
        }
    }

    public async Task<string> RemuxToMp4Async(
        CancellationToken ct = default)
    {
        if (_currentOptions is null)
        {
            throw new InvalidOperationException(
                "No recording session active.");
        }

        SetState(RecordingState.Remuxing);

        var segments =
            GetSegments()
                .Select(segment => segment.FilePath)
                .ToList();

        if (segments.Count == 0)
        {
            throw new InvalidOperationException(
                "No segments to remux.");
        }

        var concatFile = Path.Combine(
            _currentOptions.OutputDirectory,
            "concat.txt");

        await File.WriteAllLinesAsync(
            concatFile,
            segments.Select(
                segment =>
                    $"file '{segment.Replace("'", "'\\''")}'"),
            ct);

        var outputPath = Path.Combine(
            _currentOptions.OutputDirectory,
            $"{_currentOptions.MatchId}_final.mp4");

        var args =
            $"-f concat -safe 0 -i \"{concatFile}\" -c copy \"{outputPath}\"";

        _logger.LogInformation(
            "Remuxing recording to MP4: {Output}",
            outputPath);

        using var proc =
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

        if (proc is null)
        {
            throw new InvalidOperationException(
                "Unable to start FFmpeg remux process.");
        }

        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"FFmpeg remux failed with exit code {proc.ExitCode}.");
        }

        File.Delete(concatFile);

        SetState(RecordingState.Complete);

        _logger.LogInformation(
            "Remux complete: {Path}",
            outputPath);

        return outputPath;
    }

    public async Task RecoverAsync(
        string matchId,
        CancellationToken ct = default)
    {
        if (_state == RecordingState.Recording)
        {
            _logger.LogInformation(
                "Recording recovery skipped because FFmpeg is already recording.");

            return;
        }

        SetState(RecordingState.Recovering);

        var session =
            await _sessionStore.GetActiveForMatchAsync(
                matchId,
                ct);

        if (session is null)
        {
            _logger.LogInformation(
                "No recoverable recording session found.");

            SetState(RecordingState.Idle);

            return;
        }

        _logger.LogInformation(
            "Recovering recording session {RecordingId} for match {MatchId}; persisted state={State}",
            session.RecordingId,
            session.MatchId,
            session.State);

        _currentSession = session;

        _currentOptions = new RecordingOptions(
            session.MatchId,
            session.CameraId,
            session.RtspUrl,
            session.OutputDirectory,
            session.SegmentDurationSeconds,
            session.IncludeAudio);

        try
        {
            Directory.CreateDirectory(
                _currentOptions.OutputDirectory);

            var existingSegments =
                GetSegments();

            _logger.LogInformation(
                "Recording recovery found {Count} existing MKV segment(s) for match {MatchId}",
                existingSegments.Count,
                session.MatchId);

            session.State =
                RecordingState.Recovering;

            session.SegmentCount =
                existingSegments.Count;

            session.UpdatedAt =
                DateTimeOffset.UtcNow;

            await _sessionStore.SaveAsync(
                session,
                ct);

            var nextSegmentNumber =
                GetNextSegmentNumber(
                    _currentOptions.MatchId,
                    _currentOptions.OutputDirectory);

            var outputPattern = Path.Combine(
                _currentOptions.OutputDirectory,
                $"{_currentOptions.MatchId}_%03d.mkv");

            if (_camera.ConnectionState !=
                CameraConnectionState.Connected)
            {
                await _camera.ConnectAsync(ct);
            }

            // Resolve fresh credentials at runtime.
            // Never reuse/persist credentials from the session.
            var runtimeRtspUrl =
                await _camera.GetAuthenticatedStreamUrlAsync(
                    null,
                    ct);

            var mapArgs =
                _currentOptions.IncludeAudio
                    ? "-map 0:v:0 -map 0:a?"
                    : "-map 0:v:0";

            var audioArgs =
                _currentOptions.IncludeAudio
                    ? "-c:a copy"
                    : "-an";

            var args = string.Join(
                " ",
                "-rtsp_transport tcp",
                $"-i \"{runtimeRtspUrl}\"",
                mapArgs,
                "-c:v copy",
                "-bsf:v \"setts=pts='if(eq(PTS,NOPTS),N*3600,PTS)':dts='if(eq(DTS,NOPTS),(N-1)*3600,DTS)'\"",
                "-avoid_negative_ts make_zero",
                audioArgs,
                "-f segment",
                $"-segment_time {_currentOptions.SegmentDurationSeconds}",
                "-segment_format mkv",
                "-reset_timestamps 1",
                $"-segment_start_number {nextSegmentNumber}",
                $"\"{outputPattern}\"");

            _logger.LogInformation(
                "Restarting FFmpeg for recovered recording session {RecordingId} using camera {CameraId}",
                session.RecordingId,
                session.CameraId);

            _ffmpegProcess = CreateFfmpegProcess(args);

            _ffmpegProcess.Start();

            _ffmpegStderrTask =
                _ffmpegProcess.StandardError.ReadToEndAsync();

            _ffmpegProcess.EnableRaisingEvents = true;

            _elapsedStopwatch.Restart();

            SetState(RecordingState.Recording);

            session.State =
                RecordingState.Recording;

            session.SegmentCount =
                GetSegments().Count;

            session.UpdatedAt =
                DateTimeOffset.UtcNow;

            await _sessionStore.SaveAsync(
                session,
                ct);

            _logger.LogInformation(
                "Recording session {RecordingId} recovered successfully for match {MatchId}; existing segments preserved={SegmentCount}",
                session.RecordingId,
                session.MatchId,
                existingSegments.Count);
        }
        catch (Exception ex)
        {
            _elapsedStopwatch.Stop();

            SetState(RecordingState.Error);

            session.State =
                RecordingState.Error;

            session.SegmentCount =
                GetSegments().Count;

            session.UpdatedAt =
                DateTimeOffset.UtcNow;

            await _sessionStore.SaveAsync(
                session,
                CancellationToken.None);

            _logger.LogError(
                ex,
                "Failed to recover recording session {RecordingId} for match {MatchId}",
                session.RecordingId,
                session.MatchId);

            throw;
        }
    }

    public IReadOnlyList<RecordingSegmentInfo>
        GetSegments()
    {
        if (_currentOptions is null)
        {
            return [];
        }

        if (!Directory.Exists(
            _currentOptions.OutputDirectory))
        {
            return [];
        }

        var files = Directory
            .GetFiles(
                _currentOptions.OutputDirectory,
                $"{_currentOptions.MatchId}_*.mkv")
            .OrderBy(file => file)
            .ToList();

        var activeOrInterruptedTail =
            _state == RecordingState.Recording ||
            _state == RecordingState.Starting ||
            _state == RecordingState.Rotating ||
            _state == RecordingState.Recovering ||
            _state == RecordingState.Error;

        return files
            .Select(
                (file, index) =>
                {
                    var info =
                        new FileInfo(file);

                    var isLast =
                        index == files.Count - 1;

                    var isComplete =
                        !(activeOrInterruptedTail && isLast);

                    return new RecordingSegmentInfo(
                        index + 1,
                        file,
                        info.CreationTimeUtc,
                        info.LastWriteTimeUtc,
                        isComplete,
                        null,
                        info.Length);
                })
            .ToList();
    }

    private static int GetNextSegmentNumber(
        string matchId,
        string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            return 0;
        }

        var prefix =
            $"{matchId}_";

        var maxSegmentNumber = -1;

        foreach (var file in Directory.GetFiles(
                     outputDirectory,
                     $"{matchId}_*.mkv"))
        {
            var name =
                Path.GetFileNameWithoutExtension(file);

            if (!name.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix =
                name[prefix.Length..];

            if (int.TryParse(
                    suffix,
                    out var segmentNumber))
            {
                maxSegmentNumber =
                    Math.Max(
                        maxSegmentNumber,
                        segmentNumber);
            }
        }

        return maxSegmentNumber + 1;
    }

    public async Task<string> GetChecksumAsync(
        string filePath,
        CancellationToken ct = default)
    {
        using var stream =
            File.OpenRead(filePath);

        var hash =
            await System.Security.Cryptography
                .SHA256
                .HashDataAsync(
                    stream,
                    ct);

        return Convert
            .ToHexString(hash)
            .ToLowerInvariant();
    }

    private Process CreateFfmpegProcess(
        string args)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = false
        };

        process.Exited +=
            OnFfmpegExited;

        return process;
    }

    private void OnFfmpegExited(
        object? sender,
        EventArgs e)
    {
        _ = HandleFfmpegExitedAsync();
    }

    private async Task HandleFfmpegExitedAsync()
    {
        if (_state != RecordingState.Recording)
        {
            return;
        }

        _elapsedStopwatch.Stop();

        int? exitCode = null;
        string stderr = string.Empty;

        try
        {
            var process = _ffmpegProcess;

            if (process is not null)
            {
                if (process.HasExited)
                {
                    exitCode = process.ExitCode;
                }

                if (_ffmpegStderrTask is not null)
                {
                    stderr =
                        await _ffmpegStderrTask;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to read FFmpeg exit diagnostics");
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            var safeStderr =
                RedactRtspCredentials(stderr);

            _logger.LogWarning(
                "FFmpeg stderr:{NewLine}{Stderr}",
                Environment.NewLine,
                safeStderr);
        }

        _logger.LogWarning(
            "FFmpeg exited unexpectedly during recording; exit code={ExitCode}",
            exitCode);

        SetState(RecordingState.Error);

        if (_currentSession is null)
        {
            return;
        }

        _currentSession.State =
            RecordingState.Error;

        _currentSession.SegmentCount =
            GetSegments().Count;

        _currentSession.UpdatedAt =
            DateTimeOffset.UtcNow;

        await PersistUnexpectedExitAsync(
            _currentSession);
    }

    private static string RedactRtspCredentials(
        string value)
    {
        return Regex.Replace(
            value,
            @"rtsp://[^/@\s]+:[^/@\s]+@",
            "rtsp://***:***@",
            RegexOptions.IgnoreCase);
    }

    private async Task PersistUnexpectedExitAsync(
        RecordingSession session)
    {
        try
        {
            await _sessionStore.SaveAsync(
                session,
                CancellationToken.None);

            _logger.LogWarning(
                "Recording session {RecordingId} persisted as Error after unexpected FFmpeg exit",
                session.RecordingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist recording session {RecordingId} after unexpected FFmpeg exit",
                session.RecordingId);
        }
    }

    private void SetState(
        RecordingState state)
    {
        lock (_lock)
        {
            _state = state;
        }

        StateChanged?.Invoke(state);
    }

    public async ValueTask DisposeAsync()
    {
        if (_ffmpegProcess is
            { HasExited: false })
        {
            await StopRecordingAsync();
        }
    }
}
