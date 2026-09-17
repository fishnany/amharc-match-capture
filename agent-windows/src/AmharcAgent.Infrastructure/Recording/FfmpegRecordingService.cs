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
    private readonly IStreamReceiverMediaSource _mediaSource;
    private readonly IRecordingAudioSourceResolver _audioSourceResolver;
    private readonly string _ffmpegPath;
    private readonly IRecordingProcessControl _processControl;

    private Process? _ffmpegProcess;
    private Task<string>? _ffmpegStderrTask;
    private Stream? _ffmpegStandardInput;
    private StreamReader? _ffmpegStandardError;
    private IStreamReceiverMediaLease? _mediaLease;
    private CancellationTokenSource? _mediaPumpCts;
    private Task? _mediaPumpTask;
    private readonly Stopwatch _elapsedStopwatch = new();

    private RecordingState _state = RecordingState.Idle;
    private RecordingOptions? _currentOptions;
    private RecordingSession? _currentSession;

    private readonly object _lock = new();

    public event Action<RecordingState>? StateChanged;

    public FfmpegRecordingService(
        ILogger<FfmpegRecordingService> logger,
        IRecordingSessionStore sessionStore,
        IStreamReceiverMediaSource mediaSource,
        IRecordingAudioSourceResolver audioSourceResolver,
        string ffmpegPath = "ffmpeg.exe",
        IRecordingProcessControl? processControl = null)
    {
        _logger = logger;
        _sessionStore = sessionStore;
        _mediaSource = mediaSource;
        _audioSourceResolver = audioSourceResolver;
        _ffmpegPath = ffmpegPath;
        _processControl = processControl ?? new WindowsRecordingProcessControl();
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

            _mediaLease =
                await _mediaSource.AcquireAsync(ct, lossIntolerant: true);

            var inputArgs =
                await BuildRecordingInputArgumentsAsync(
                    options.IncludeAudio,
                    ct);
            var mapArgs = BuildRecordingMapArguments(options.IncludeAudio);
            var audioArgs = BuildRecordingAudioArguments(options.IncludeAudio);

            var args = string.Join(
                " ",
                inputArgs,
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

            var launchedProcess =
                _processControl.Start(
                    _ffmpegPath,
                    args,
                    OnFfmpegExited);

            _ffmpegProcess = launchedProcess.Process;
            _ffmpegStandardInput = launchedProcess.StandardInput;
            _ffmpegStandardError = launchedProcess.StandardError;

            StartMediaPump(
                _ffmpegStandardInput,
                _mediaLease,
                ct);

            _ffmpegStderrTask =
                _ffmpegStandardError.ReadToEndAsync();

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
            await StopMediaPumpAsync();
            await ReleaseMediaLeaseAsync();

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

        var process = _ffmpegProcess;
        Exception? shutdownError = null;

        try
        {
            // FFmpeg stdin is the canonical MPEG-TS video input in MR-16D.
            // Pump shutdown closes stdin and therefore signals clean EOF.
            // A pump-shutdown fault must never bypass FFmpeg termination.
            try
            {
                await StopMediaPumpAsync();
            }
            catch (Exception ex)
            {
                shutdownError = ex;

                _logger.LogError(
                    ex,
                    "Error stopping canonical recording media pump");
            }

            if (!process.HasExited)
            {
                _logger.LogInformation(
                    "Canonical recording media EOF delivered; requesting graceful FFmpeg process-group termination");

                try
                {
                    _processControl.RequestGracefulTermination(process.Id);
                }
                catch (Exception ex)
                {
                    shutdownError ??= ex;

                    _logger.LogError(
                        ex,
                        "Unable to request graceful FFmpeg process-group termination");
                }
            }

            var gracefulExitConfirmed =
                process.HasExited ||
                process.WaitForExit(5_000);

            if (!gracefulExitConfirmed)
            {
                shutdownError ??= new TimeoutException(
                    "FFmpeg did not exit within the bounded graceful termination window.");
            }

            if (shutdownError is not null &&
                !process.HasExited)
            {
                _logger.LogWarning(
                    "Graceful FFmpeg shutdown failed; killing process tree and classifying Recording as Error");

                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }

            if (!process.HasExited)
            {
                throw new InvalidOperationException(
                    "FFmpeg recording process termination could not be confirmed.");
            }

            if (_ffmpegStderrTask is not null)
            {
                await _ffmpegStderrTask;
            }

            if (shutdownError is not null)
            {
                throw new InvalidOperationException(
                    "Canonical recording media pump failed during shutdown.",
                    shutdownError);
            }

            _elapsedStopwatch.Stop();

            await ReleaseMediaLeaseAsync();

            _ffmpegStandardInput?.Dispose();
            _ffmpegStandardInput = null;
            _ffmpegStandardError?.Dispose();
            _ffmpegStandardError = null;
            process.Dispose();
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
                "Recording stopped after FFmpeg termination was confirmed. Elapsed: {Seconds:F1}s",
                ElapsedSeconds);
        }
        catch (Exception ex)
        {
            _elapsedStopwatch.Stop();

            await ReleaseMediaLeaseAsync();

            if (process.HasExited)
            {
                _ffmpegStandardInput?.Dispose();
                _ffmpegStandardInput = null;
                _ffmpegStandardError?.Dispose();
                _ffmpegStandardError = null;
                process.Dispose();
                _ffmpegProcess = null;
                _ffmpegStderrTask = null;
            }

            SetState(RecordingState.Error);

            if (_currentSession is not null)
            {
                _currentSession.State =
                    RecordingState.Error;

                _currentSession.SegmentCount =
                    GetSegments().Count;

                _currentSession.UpdatedAt =
                    DateTimeOffset.UtcNow;

                await _sessionStore.SaveAsync(
                    _currentSession,
                    CancellationToken.None);
            }

            _logger.LogError(
                ex,
                "Recording stop failed; recording was not marked Complete");

            throw;
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

            _mediaLease =
                await _mediaSource.AcquireAsync(ct, lossIntolerant: true);

            var inputArgs =
                await BuildRecordingInputArgumentsAsync(
                    _currentOptions.IncludeAudio,
                    ct);
            var mapArgs = BuildRecordingMapArguments(_currentOptions.IncludeAudio);
            var audioArgs = BuildRecordingAudioArguments(_currentOptions.IncludeAudio);

            var args = string.Join(
                " ",
                inputArgs,
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

            var launchedProcess =
                _processControl.Start(
                    _ffmpegPath,
                    args,
                    OnFfmpegExited);

            _ffmpegProcess = launchedProcess.Process;
            _ffmpegStandardInput = launchedProcess.StandardInput;
            _ffmpegStandardError = launchedProcess.StandardError;

            StartMediaPump(
                _ffmpegStandardInput,
                _mediaLease,
                ct);

            _ffmpegStderrTask =
                _ffmpegStandardError.ReadToEndAsync();

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
            await StopMediaPumpAsync();
            await ReleaseMediaLeaseAsync();

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

    private async Task<string> BuildRecordingInputArgumentsAsync(
        bool includeAudio,
        CancellationToken ct)
    {
        const string canonicalVideoInput =
            "-f mpegts -i pipe:0";

        if (!includeAudio)
        {
            return canonicalVideoInput;
        }

        var audio = await _audioSourceResolver.ResolveAsync(ct);
        if (!audio.IsAvailable || audio.Credential is null)
        {
            throw new InvalidOperationException(
                "Authoritative recording audio source is unavailable; recording will not fall back to camera audio.");
        }

        var audioRuntimeRtspUrl = BuildAuthenticatedRtspUrl(
            audio.Endpoint,
            audio.Port,
            audio.PresentationPath,
            audio.Credential);

        return string.Join(
            " ",
            canonicalVideoInput,
            "-rtsp_transport tcp",
            $"-i \"{audioRuntimeRtspUrl}\"");
    }

    private static string BuildRecordingMapArguments(bool includeAudio)
    {
        return includeAudio
            ? "-map 0:v:0 -map 1:a:0"
            : "-map 0:v:0";
    }

    private static string BuildRecordingAudioArguments(bool includeAudio)
    {
        return includeAudio
            ? "-c:a copy"
            : "-an";
    }

    private static string BuildAuthenticatedRtspUrl(
        string endpoint,
        int port,
        string presentationPath,
        AudioCredential credential)
    {
        var path = presentationPath.StartsWith("/", StringComparison.Ordinal)
            ? presentationPath
            : "/" + presentationPath;

        var user = Uri.EscapeDataString(credential.Username);
        var password = Uri.EscapeDataString(credential.Password);

        return $"rtsp://{user}:{password}@{endpoint}:{port}{path}";
    }
    private void StartMediaPump(
        Stream destination,
        IStreamReceiverMediaLease? lease,
        CancellationToken ct)
    {
        if (lease is null)
        {
            throw new InvalidOperationException(
                "Canonical recording media lease was not acquired.");
        }

        _mediaPumpCts =
            CancellationTokenSource.CreateLinkedTokenSource(ct);

        _mediaPumpTask =
            PumpCanonicalMediaAsync(
                lease.Stream,
                destination,
                _mediaPumpCts.Token);
    }

    private async Task PumpCanonicalMediaAsync(
        Stream source,
        Stream destination,
        CancellationToken ct)
    {
        try
        {
            await source.CopyToAsync(
                destination,
                64 * 1024,
                ct);

            await destination.FlushAsync(ct);
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
        }
        catch (IOException)
            when (_ffmpegProcess is null ||
                  _ffmpegProcess.HasExited)
        {
        }
        finally
        {
            try
            {
                destination.Close();
            }
            catch
            {
            }
        }
    }

    private async Task StopMediaPumpAsync()
    {
        var cts = _mediaPumpCts;
        var task = _mediaPumpTask;

        _mediaPumpCts = null;
        _mediaPumpTask = null;

        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        if (task is not null)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
        }

        cts?.Dispose();
    }

    private async Task ReleaseMediaLeaseAsync()
    {
        var lease = _mediaLease;
        _mediaLease = null;

        if (lease is not null)
        {
            await lease.DisposeAsync();
        }
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
        await StopMediaPumpAsync();
        await ReleaseMediaLeaseAsync();

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
