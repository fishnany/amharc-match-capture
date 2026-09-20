using System.Diagnostics;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Streaming;

/// <summary>
/// Publishes canonical AMHARC live-video media to an RTMP destination.
///
/// The service is a consumer of IStreamReceiverMediaSource. It never
/// acquires the authenticated camera source directly and therefore never
/// receives camera credentials or an RTSP source URI.
/// </summary>
public sealed class RtmpStreamingService :
    IStreamingService,
    IAsyncDisposable
{
    private const int MaxReconnects = 3;

    private readonly ILogger<RtmpStreamingService> _logger;
    private readonly IStreamReceiverMediaSource _mediaSource;
    private readonly IRecordingAudioSourceResolver _audioSourceResolver;
    private readonly string _ffmpegPath;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

    private Process? _ffmpegProcess;
    private IStreamReceiverMediaLease? _mediaLease;
    private CancellationTokenSource? _sessionCts;
    private Task? _mediaPumpTask;
    private Task? _exitObservationTask;

    private StreamingState _state = StreamingState.Idle;
    private readonly Stopwatch _uptime = new();
    private StreamingDestinationConfig? _activeDestination;
    private int _reconnectCount;
    private DateTimeOffset? _startedAt;
    private string? _lastError;
    private int _disposeState;

    public RtmpStreamingService(
        ILogger<RtmpStreamingService> logger,
        IStreamReceiverMediaSource mediaSource,
        IRecordingAudioSourceResolver audioSourceResolver,
        string ffmpegPath = "ffmpeg.exe")
    {
        _logger = logger;
        _mediaSource = mediaSource;
        _audioSourceResolver =
            audioSourceResolver ??
            throw new ArgumentNullException(nameof(audioSourceResolver));
        _ffmpegPath =
            string.IsNullOrWhiteSpace(ffmpegPath)
                ? throw new ArgumentException(
                    "Managed FFmpeg path is required.",
                    nameof(ffmpegPath))
                : ffmpegPath;
    }

    public StreamingState State => _state;

    public StreamingStats? Stats =>
        _state is
            StreamingState.Connecting or
            StreamingState.Streaming or
            StreamingState.Reconnecting or
            StreamingState.Stopping
            ? new StreamingStats(
                _uptime.Elapsed.TotalSeconds,
                0,
                0,
                _reconnectCount)
            : null;

    public string? ActiveDestinationId =>
        _activeDestination?.DestinationId;

    public DateTimeOffset? StartedAt =>
        _startedAt;

    public string? LastError =>
        _lastError;

    public event Action<StreamingState>? StateChanged;

    public event Action<Exception>? ErrorOccurred;

    public async Task StartAsync(
        StreamingDestinationConfig destination,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        await _lifecycleGate.WaitAsync(ct);

        try
        {
            if (_state is
                StreamingState.Connecting or
                StreamingState.Streaming or
                StreamingState.Reconnecting)
            {
                throw new InvalidOperationException(
                    "Streaming is already active.");
            }

            _activeDestination = destination;
            _reconnectCount = 0;
            _lastError = null;
            _startedAt = DateTimeOffset.UtcNow;

            _sessionCts =
                CancellationTokenSource
                    .CreateLinkedTokenSource(ct);

            await StartFfmpegAsync(
                destination,
                _sessionCts.Token);
        }
        catch
        {
            await CleanupSessionAsync(
                clearDestination: true);

            throw;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAsync(
        CancellationToken ct = default)
    {
        await _lifecycleGate.WaitAsync(ct);

        try
        {
            if (_state == StreamingState.Idle &&
                _ffmpegProcess is null &&
                _mediaLease is null)
            {
                return;
            }

            SetState(StreamingState.Stopping);

            await CleanupRuntimeAsync();

            _sessionCts?.Cancel();
            _sessionCts?.Dispose();
            _sessionCts = null;

            _uptime.Stop();
            _activeDestination = null;
            _startedAt = null;

            SetState(StreamingState.Idle);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task StartFfmpegAsync(
        StreamingDestinationConfig destination,
        CancellationToken ct)
    {
        SetState(
            _reconnectCount == 0
                ? StreamingState.Connecting
                : StreamingState.Reconnecting);

        var lease =
            await _mediaSource.AcquireAsync(
                ct,
                lossIntolerant: false,
                startMode:
                    StreamReceiverMediaStartMode.LiveAligned);

        Process? process = null;

        try
        {
            var arguments =
                await BuildStreamingArgumentsAsync(destination, ct);

            process =
                new Process
                {
                    StartInfo =
                        new ProcessStartInfo
                        {
                            FileName = _ffmpegPath,
                            Arguments = arguments,
                            UseShellExecute = false,
                            RedirectStandardInput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        },
                    EnableRaisingEvents = true
                };

            process.ErrorDataReceived +=
                OnFfmpegStderr;

            if (!process.Start())
            {
                throw new InvalidOperationException(
                    "Unable to start RTMP streaming FFmpeg process.");
            }

            process.BeginErrorReadLine();

            _mediaLease = lease;
            _ffmpegProcess = process;

            _mediaPumpTask =
                PumpCanonicalMediaAsync(
                    lease,
                    process,
                    ct);

            _exitObservationTask =
                ObserveFfmpegExitAsync(
                    process,
                    ct);

            _uptime.Restart();

            SetState(StreamingState.Streaming);

            _logger.LogInformation(
                "Streaming canonical AMHARC media to {Platform} ({Url}).",
                destination.Platform,
                destination.ServerUrl);
        }
        catch
        {
            if (process is not null)
            {
                process.ErrorDataReceived -=
                    OnFfmpegStderr;

                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(
                            entireProcessTree: true);

                        await process.WaitForExitAsync(
                            CancellationToken.None);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Process exited between checks.
                }

                process.Dispose();
            }

            await lease.DisposeAsync();

            throw;
        }
    }

    private async Task PumpCanonicalMediaAsync(
        IStreamReceiverMediaLease lease,
        Process process,
        CancellationToken ct)
    {
        try
        {
            await lease.Stream.CopyToAsync(
                process.StandardInput.BaseStream,
                ct);

            await process.StandardInput.BaseStream
                .FlushAsync(ct);
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            // Expected during StopAsync.
        }
        catch (IOException)
            when (process.HasExited)
        {
            // FFmpeg closed stdin because it exited.
            // Exit observation owns reconnect/error handling.
        }
        catch (ObjectDisposedException)
            when (ct.IsCancellationRequested ||
                  process.HasExited)
        {
            // Expected while the streaming runtime is being torn down.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Canonical media pump to RTMP FFmpeg failed.");

            _lastError = ex.Message;
        }
    }

    private async Task ObserveFfmpegExitAsync(
        Process process,
        CancellationToken ct)
    {
        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            return;
        }

        if (ct.IsCancellationRequested ||
            _state is
                StreamingState.Stopping or
                StreamingState.Idle)
        {
            return;
        }

        await HandleUnexpectedExitAsync(
            process.ExitCode,
            ct);
    }

    private async Task HandleUnexpectedExitAsync(
        int exitCode,
        CancellationToken ct)
    {
        await _lifecycleGate.WaitAsync(
            CancellationToken.None);

        try
        {
            if (ct.IsCancellationRequested ||
                _state is
                    StreamingState.Stopping or
                    StreamingState.Idle)
            {
                return;
            }

            _logger.LogWarning(
                "Streaming FFmpeg exited unexpectedly with code {ExitCode}.",
                exitCode);

            await CleanupRuntimeAsync(
                skipExitObservationAwait: true);

            if (_reconnectCount < MaxReconnects &&
                _activeDestination is not null &&
                !ct.IsCancellationRequested)
            {
                _reconnectCount++;

                SetState(
                    StreamingState.Reconnecting);

                _logger.LogInformation(
                    "Attempting streaming reconnect {Attempt}/{Max}.",
                    _reconnectCount,
                    MaxReconnects);

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(5),
                        ct);

                    await StartFfmpegAsync(
                        _activeDestination,
                        ct);

                    return;
                }
                catch (OperationCanceledException)
                    when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;

                    _logger.LogWarning(
                        ex,
                        "Streaming reconnect attempt {Attempt}/{Max} failed.",
                        _reconnectCount,
                        MaxReconnects);

                    if (_reconnectCount < MaxReconnects)
                    {
                        _ = Task.Run(
                            () => RetryAfterFailedStartAsync(ct),
                            CancellationToken.None);

                        return;
                    }
                }
            }

            _lastError ??=
                "Max streaming reconnects exceeded.";

            SetState(StreamingState.Error);

            ErrorOccurred?.Invoke(
                new InvalidOperationException(
                    _lastError));
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task RetryAfterFailedStartAsync(
        CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return;
        }

        await _lifecycleGate.WaitAsync(
            CancellationToken.None);

        try
        {
            if (ct.IsCancellationRequested ||
                _activeDestination is null ||
                _state is
                    StreamingState.Stopping or
                    StreamingState.Idle)
            {
                return;
            }

            while (_reconnectCount < MaxReconnects)
            {
                _reconnectCount++;

                SetState(
                    StreamingState.Reconnecting);

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(5),
                        ct);

                    await StartFfmpegAsync(
                        _activeDestination,
                        ct);

                    return;
                }
                catch (OperationCanceledException)
                    when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;

                    _logger.LogWarning(
                        ex,
                        "Streaming reconnect attempt {Attempt}/{Max} failed.",
                        _reconnectCount,
                        MaxReconnects);

                    await CleanupRuntimeAsync();
                }
            }

            _lastError =
                "Max streaming reconnects exceeded.";

            SetState(StreamingState.Error);

            ErrorOccurred?.Invoke(
                new InvalidOperationException(
                    _lastError));
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task CleanupRuntimeAsync(
        bool skipExitObservationAwait = false)
    {
        var process = _ffmpegProcess;
        var lease = _mediaLease;
        var pumpTask = _mediaPumpTask;
        var exitTask = _exitObservationTask;

        _ffmpegProcess = null;
        _mediaLease = null;
        _mediaPumpTask = null;
        _exitObservationTask = null;

        if (process is not null)
        {
            process.ErrorDataReceived -=
                OnFfmpegStderr;

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(
                        entireProcessTree: true);

                    await process.WaitForExitAsync(
                        CancellationToken.None);
                }
            }
            catch (InvalidOperationException)
            {
                // Process exited between checks.
            }
        }

        if (lease is not null)
        {
            await lease.DisposeAsync();
        }

        if (pumpTask is not null)
        {
            try
            {
                await pumpTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
            catch (IOException)
            {
                // Expected when FFmpeg closes its input.
            }
            catch (ObjectDisposedException)
            {
                // Expected when the lease is detached.
            }
        }


        process?.Dispose();
    }

    private async Task CleanupSessionAsync(
        bool clearDestination)
    {
        _sessionCts?.Cancel();

        await CleanupRuntimeAsync();

        _sessionCts?.Dispose();
        _sessionCts = null;

        _uptime.Stop();

        if (clearDestination)
        {
            _activeDestination = null;
            _startedAt = null;
        }

        SetState(StreamingState.Idle);
    }

    private async Task<string> BuildStreamingArgumentsAsync(
        StreamingDestinationConfig destination,
        CancellationToken ct)
    {
        var audio =
            await _audioSourceResolver.ResolveAsync(ct);

        if (!audio.IsAvailable ||
            audio.Credential is null)
        {
            throw new InvalidOperationException(
                "Authoritative audio source is unavailable; streaming will not start without audio.");
        }

        var bitrate =
            destination.BitRate ?? 4000;

        var resolution =
            destination.Resolution ?? "1920x1080";

        var fps =
            destination.FrameRate ?? 25;

        var rtmpTarget =
            $"{destination.ServerUrl.TrimEnd('/')}/{destination.StreamKey}";

        var audioRuntimeRtspUrl =
            BuildAuthenticatedRtspUrl(
                audio.Endpoint,
                audio.Port,
                audio.PresentationPath,
                audio.Credential);

        return string.Join(
            " ",
            "-hide_banner",
            "-loglevel warning",

            // Input 0: canonical AMHARC video.
            "-f mpegts",
            "-i pipe:0",

            // Input 1: authoritative AMHARC audio.
            "-rtsp_transport tcp",
            $"-i \"{audioRuntimeRtspUrl}\"",

            "-map 0:v:0",
            "-map 1:a:0",

            "-c:v libx264",
            "-preset veryfast",
            $"-b:v {bitrate}k",
            $"-maxrate {bitrate}k",
            $"-bufsize {bitrate * 2}k",
            $"-vf scale={resolution}",
            $"-r {fps}",

            "-c:a aac",
            "-b:a 128k",

            "-f flv",
            $"\"{rtmpTarget}\"");
    }

    private static string BuildAuthenticatedRtspUrl(
        string endpoint,
        int port,
        string presentationPath,
        AudioCredential credential)
    {
        var path =
            presentationPath.StartsWith(
                "/",
                StringComparison.Ordinal)
                ? presentationPath
                : "/" + presentationPath;

        var user =
            Uri.EscapeDataString(
                credential.Username);

        var password =
            Uri.EscapeDataString(
                credential.Password);

        return
            $"rtsp://{user}:{password}@{endpoint}:{port}{path}";
    }

    private void OnFfmpegStderr(
        object sender,
        DataReceivedEventArgs e)
    {
        if (e.Data is null)
        {
            return;
        }

        _logger.LogTrace(
            "Streaming FFmpeg: {Line}",
            e.Data);
    }

    private void SetState(
        StreamingState state)
    {
        _state = state;
        StateChanged?.Invoke(state);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref _disposeState,
                1) != 0)
        {
            return;
        }

        try
        {
            await StopAsync();
        }
        finally
        {
            _lifecycleGate.Dispose();
        }
    }
}
