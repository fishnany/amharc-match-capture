using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Media;

/// <summary>
/// Production foundation for canonical AMHARC live-video media ingress.
///
/// MR-16B deliberately establishes lifecycle, media validation, health and
/// recovery without migrating the proven preview or recording consumers.
/// The authenticated RTSP URI remains runtime-only inside this boundary.
/// </summary>
public sealed class FfmpegStreamReceiver : IStreamReceiver, IAsyncDisposable
{
    private const int StartupTimeoutSeconds = 10;
    private const int MaxRecoveryAttempts = 3;

    private readonly ICameraAdapter _camera;
    private readonly ILogger<FfmpegStreamReceiver> _logger;
    private readonly string _ffmpegPath;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly object _stateLock = new();

    private Process? _process;
    private CancellationTokenSource? _lifetimeCts;
    private Task? _progressTask;
    private Task? _stderrTask;
    private Task? _exitTask;

    private StreamReceiverState _state = StreamReceiverState.Idle;
    private StreamReceiverHealth _health;

    public FfmpegStreamReceiver(
        ICameraAdapter camera,
        ILogger<FfmpegStreamReceiver> logger,
        string ffmpegPath)
    {
        _camera = camera;
        _logger = logger;
        _ffmpegPath = string.IsNullOrWhiteSpace(ffmpegPath)
            ? throw new ArgumentException(
                "Managed FFmpeg path is required.",
                nameof(ffmpegPath))
            : ffmpegPath;

        _health = new StreamReceiverHealth(
            StreamReceiverState.Idle,
            camera.CameraId,
            null,
            null,
            null,
            DateTimeOffset.UtcNow);
    }

    public StreamReceiverState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    public StreamReceiverHealth Health
    {
        get
        {
            lock (_stateLock)
            {
                return _health;
            }
        }
    }

    public event Action<StreamReceiverState>? StateChanged;
    public event Action<StreamReceiverHealth>? HealthChanged;

    public async Task StartAsync(
        CancellationToken ct = default)
    {
        await _lifecycleGate.WaitAsync(ct);

        try
        {
            if (State is StreamReceiverState.Available or
                StreamReceiverState.Starting)
            {
                return;
            }

            SetState(StreamReceiverState.Starting);

            try
            {
                await StartCoreAsync(ct);
            }
            catch
            {
                await StopProcessCoreAsync();
                SetState(StreamReceiverState.Error);
                throw;
            }
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
            if (State == StreamReceiverState.Idle &&
                _process is null)
            {
                return;
            }

            SetState(StreamReceiverState.Stopping);
            await StopProcessCoreAsync();
            SetState(StreamReceiverState.Idle);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task RecoverAsync(
        CancellationToken ct = default)
    {
        await _lifecycleGate.WaitAsync(ct);

        try
        {
            if (State == StreamReceiverState.Available)
            {
                return;
            }

            SetState(StreamReceiverState.Recovering);

            Exception? lastError = null;

            for (var attempt = 1;
                 attempt <= MaxRecoveryAttempts;
                 attempt++)
            {
                ct.ThrowIfCancellationRequested();

                await StopProcessCoreAsync();

                if (attempt > 1)
                {
                    var delay =
                        TimeSpan.FromSeconds(
                            Math.Pow(2, attempt - 2));

                    await Task.Delay(delay, ct);
                }

                try
                {
                    await StartCoreAsync(
                        ct,
                        preserveRecoveringState: true);

                    return;
                }
                catch (Exception ex)
                    when (ex is not OperationCanceledException)
                {
                    lastError = ex;

                    _logger.LogWarning(
                        "Canonical media recovery attempt {Attempt}/{Max} failed ({ErrorType}).",
                        attempt,
                        MaxRecoveryAttempts,
                        ex.GetType().Name);
                }
            }

            SetState(StreamReceiverState.Error);

            throw new InvalidOperationException(
                "Canonical live-video media recovery failed after bounded retries.",
                lastError);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task StartCoreAsync(
        CancellationToken ct,
        bool preserveRecoveringState = false)
    {
        if (!preserveRecoveringState &&
            State != StreamReceiverState.Starting)
        {
            SetState(StreamReceiverState.Starting);
        }

        if (_camera.ConnectionState !=
            CameraConnectionState.Connected)
        {
            await _camera.ConnectAsync(ct);
        }

        var runtimeRtspUrl =
            await _camera.GetAuthenticatedStreamUrlAsync(
                null,
                ct);

        var arguments =
            BuildReceiverArguments(runtimeRtspUrl);

        _lifetimeCts =
            CancellationTokenSource.CreateLinkedTokenSource(ct);

        var process =
            new Process
            {
                StartInfo =
                    new ProcessStartInfo
                    {
                        FileName = _ffmpegPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = false
                    },
                EnableRaisingEvents = true
            };

        _process = process;

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException(
                    "Unable to start canonical media receiver FFmpeg process.");
            }
        }
        catch
        {
            process.Dispose();
            _process = null;
            throw;
        }

        _logger.LogInformation(
            "Canonical AMHARC live-video media receiver started using managed FFmpeg.");

        var firstMedia =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        _progressTask =
            PumpProgressAsync(
                process,
                firstMedia,
                _lifetimeCts.Token);

        _stderrTask =
            CaptureStderrAsync(
                process,
                _lifetimeCts.Token);

        _exitTask =
            ObserveExitAsync(
                process,
                firstMedia,
                _lifetimeCts.Token);

        try
        {
            await firstMedia.Task.WaitAsync(
                TimeSpan.FromSeconds(
                    StartupTimeoutSeconds),
                ct);
        }
        catch (TimeoutException)
        {
            throw new InvalidOperationException(
                "Canonical live-video media was not established within the startup timeout.");
        }

        SetState(StreamReceiverState.Available);
    }

    private async Task PumpProgressAsync(
        Process process,
        TaskCompletionSource<bool> firstMedia,
        CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line =
                    await process.StandardOutput
                        .ReadLineAsync(ct);

                if (line is null)
                {
                    return;
                }

                if (TryApplyProgressLine(line))
                {
                    firstMedia.TrySetResult(true);
                }
            }
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            // Expected during StopAsync or retry.
        }
        catch (Exception ex)
        {
            firstMedia.TrySetException(
                new InvalidOperationException(
                    "Canonical media progress monitoring failed.",
                    ex));
        }
    }

    private async Task CaptureStderrAsync(
        Process process,
        CancellationToken ct)
    {
        try
        {
            var stderr =
                await process.StandardError
                    .ReadToEndAsync(ct);

            if (!ct.IsCancellationRequested &&
                !string.IsNullOrWhiteSpace(stderr))
            {
                var redacted =
                    RedactRtspCredentials(stderr);

                _logger.LogDebug(
                    "Canonical media receiver FFmpeg diagnostics:{NewLine}{Diagnostics}",
                    Environment.NewLine,
                    redacted);
            }
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            // Expected during StopAsync or retry.
        }
    }

    private async Task ObserveExitAsync(
        Process process,
        TaskCompletionSource<bool> firstMedia,
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

        if (ct.IsCancellationRequested)
        {
            return;
        }

        firstMedia.TrySetException(
            new InvalidOperationException(
                $"Canonical media receiver FFmpeg exited with code {process.ExitCode}."));

        if (State is StreamReceiverState.Starting or
            StreamReceiverState.Available)
        {
            SetState(StreamReceiverState.Interrupted);

            _logger.LogWarning(
                "Canonical live-video media receiver was interrupted; FFmpeg exit code={ExitCode}.",
                process.ExitCode);
        }
    }

    private bool TryApplyProgressLine(
        string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var separator = line.IndexOf('=');
        if (separator <= 0 ||
            separator >= line.Length - 1)
        {
            return false;
        }

        var key =
            line[..separator]
                .Trim();

        var value =
            line[(separator + 1)..]
                .Trim();

        var health = Health;
        var bitRate = health.BitRate;
        var frameRate = health.FrameRate;
        var droppedFrames = health.DroppedFrames;
        var mediaObserved = false;

        switch (key)
        {
            case "frame":
                if (long.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var frame) &&
                    frame > 0)
                {
                    mediaObserved = true;
                }
                break;

            case "out_time_us":
                if (long.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var outTimeUs) &&
                    outTimeUs > 0)
                {
                    mediaObserved = true;
                }
                break;

            case "fps":
                if (double.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var fps))
                {
                    frameRate = fps;
                }
                break;

            case "bitrate":
                bitRate =
                    TryParseBitRate(value) ??
                    bitRate;
                break;

            case "drop_frames":
                if (int.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var dropped))
                {
                    droppedFrames = dropped;
                }
                break;
        }

        UpdateHealth(
            bitRate,
            frameRate,
            droppedFrames);

        return mediaObserved;
    }

    private void UpdateHealth(
        double? bitRate,
        double? frameRate,
        int? droppedFrames)
    {
        StreamReceiverHealth next;

        lock (_stateLock)
        {
            next =
                new StreamReceiverHealth(
                    _state,
                    _camera.CameraId,
                    bitRate,
                    frameRate,
                    droppedFrames,
                    DateTimeOffset.UtcNow);

            _health = next;
        }

        HealthChanged?.Invoke(next);
    }

    private void SetState(
        StreamReceiverState state)
    {
        StreamReceiverHealth health;

        lock (_stateLock)
        {
            _state = state;

            health =
                _health with
                {
                    State = state,
                    Timestamp = DateTimeOffset.UtcNow
                };

            _health = health;
        }

        StateChanged?.Invoke(state);
        HealthChanged?.Invoke(health);
    }

    private async Task StopProcessCoreAsync()
    {
        var lifetimeCts = _lifetimeCts;
        var process = _process;

        _lifetimeCts = null;
        _process = null;

        try
        {
            lifetimeCts?.Cancel();

            if (process is not null)
            {
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
                    // Process already exited between checks.
                }
            }

            var tasks =
                new[]
                {
                    _progressTask,
                    _stderrTask,
                    _exitTask
                }
                .Where(task => task is not null)
                .Cast<Task>()
                .ToArray();

            if (tasks.Length > 0)
            {
                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException)
                {
                    // Expected during process teardown.
                }
                catch
                {
                    // Teardown remains best-effort; state is set by caller.
                }
            }
        }
        finally
        {
            _progressTask = null;
            _stderrTask = null;
            _exitTask = null;

            process?.Dispose();
            lifetimeCts?.Dispose();
        }
    }

    private static string BuildReceiverArguments(
        string authenticatedRtspUrl)
    {
        if (string.IsNullOrWhiteSpace(
                authenticatedRtspUrl))
        {
            throw new ArgumentException(
                "Authenticated RTSP URL is required.",
                nameof(authenticatedRtspUrl));
        }

        return string.Join(
            " ",
            "-hide_banner",
            "-loglevel warning",
            "-progress pipe:1",
            "-nostats",
            "-rtsp_transport tcp",
            "-fflags nobuffer",
            "-flags low_delay",
            "-i",
            $"\"{authenticatedRtspUrl}\"",
            "-map 0:v:0",
            "-an",
            "-c:v copy",
            "-f null",
            "-");
    }

    private static double? TryParseBitRate(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(
                value,
                "N/A",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var numeric =
            Regex.Match(
                value,
                @"[-+]?[0-9]*\.?[0-9]+");

        if (!numeric.Success ||
            !double.TryParse(
                numeric.Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return null;
        }

        return parsed;
    }

    private static string RedactRtspCredentials(
        string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return Regex.Replace(
            value,
            @"rtsp://[^/@\s]+:[^/@\s]+@",
            "rtsp://***:***@",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _lifecycleGate.Dispose();
    }
}
