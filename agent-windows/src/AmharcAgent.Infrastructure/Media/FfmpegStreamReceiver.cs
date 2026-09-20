using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Media;

/// <summary>
/// Production canonical AMHARC live-video media ingress.
///
/// The receiver is the sole owner of authenticated camera source acquisition.
/// It exposes lifecycle/health through IStreamReceiver and credential-free,
/// video-only MPEG-TS consumer leases through IStreamReceiverMediaSource.
/// </summary>
public sealed class FfmpegStreamReceiver :
    IStreamReceiver,
    IStreamReceiverMediaSource,
    IAsyncDisposable
{
    private const int StartupTimeoutSeconds = 10;
    private const int MaxRecoveryAttempts = 3;
    private const int MediaBufferSize = 64 * 1024;
    private const int ConsumerBufferChunks = 64;

    private readonly ICameraAdapter _camera;
    private readonly ILogger<FfmpegStreamReceiver> _logger;
    private readonly string _ffmpegPath;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly object _stateLock = new();
    private readonly ConcurrentDictionary<Guid, MediaConsumer> _consumers = new();


    private readonly object _mediaDispatchLock = new();
    private readonly MpegTsBootstrapBuffer _bootstrap = new();
    private Process? _process;
    private CancellationTokenSource? _lifetimeCts;
    private Task? _mediaTask;
    private Task? _diagnosticsTask;
    private Task? _exitTask;
    private int _disposeState;

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

    public async Task<IStreamReceiverMediaLease> AcquireAsync(
        CancellationToken ct = default,
        bool lossIntolerant = false,
        StreamReceiverMediaStartMode startMode =
            StreamReceiverMediaStartMode.Bootstrap)
    {
        var consumerId = Guid.NewGuid();

        var channel =
            Channel.CreateBounded<byte[]>(
                new BoundedChannelOptions(
                    ConsumerBufferChunks)
                {
                    SingleReader = true,
                    SingleWriter = true,
                    FullMode = lossIntolerant
                        ? BoundedChannelFullMode.Wait
                        : BoundedChannelFullMode.DropOldest
                });

        await StartAsync(ct);

        if (startMode ==
            StreamReceiverMediaStartMode.LiveAligned)
        {
            var liveConsumer =
                new MediaConsumer(
                    channel,
                    lossIntolerant,
                    liveBootstrap:
                        new MpegTsBootstrapBuffer());

            lock (_mediaDispatchLock)
            {
                if (!_consumers.TryAdd(
                        consumerId,
                        liveConsumer))
                {
                    channel.Writer.TryComplete();

                    throw new InvalidOperationException(
                        "Unable to register live-aligned canonical media consumer.");
                }
            }

            try
            {
                await liveConsumer.LiveReady.Task.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    ct);
            }
            catch
            {
                RemoveConsumer(consumerId);
                throw;
            }

            return new StreamReceiverMediaLease(
                new ChannelReadStream(
                    channel.Reader),
                () => RemoveConsumer(
                    consumerId));
        }

        var deadline =
            DateTime.UtcNow.AddSeconds(5);

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            lock (_mediaDispatchLock)
            {
                var bootstrap =
                    _bootstrap.Snapshot();

                if (bootstrap.Length > 0)
                {
                    if (!channel.Writer.TryWrite(
                            bootstrap))
                    {
                        throw new InvalidOperationException(
                            "Unable to prime canonical media consumer.");
                    }

                    if (!_consumers.TryAdd(
                            consumerId,
                            new MediaConsumer(
                                channel,
                                lossIntolerant,
                                liveBootstrap: null)))
                    {
                        channel.Writer.TryComplete();

                        throw new InvalidOperationException(
                            "Unable to register canonical media consumer.");
                    }

                    return new StreamReceiverMediaLease(
                        new ChannelReadStream(
                            channel.Reader),
                        () => RemoveConsumer(
                            consumerId));
                }
            }

            if (DateTime.UtcNow >= deadline)
            {
                channel.Writer.TryComplete();

                throw new InvalidOperationException(
                    "Canonical media did not produce a safe MPEG-TS late-join bootstrap within the allowed interval.");
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(25),
                ct);
        }
    }

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

            SetState(
                StreamReceiverState.Starting);

            try
            {
                await StartCoreAsync(ct);
            }
            catch
            {
                await StopProcessCoreAsync(
                    completeConsumers: false);

                SetState(
                    StreamReceiverState.Error);

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
                CompleteAllConsumers();
                return;
            }

            SetState(
                StreamReceiverState.Stopping);

            await StopProcessCoreAsync(
                completeConsumers: true);

            SetState(
                StreamReceiverState.Idle);
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
            if (State ==
                StreamReceiverState.Available)
            {
                return;
            }

            SetState(
                StreamReceiverState.Recovering);

            Exception? lastError = null;

            for (var attempt = 1;
                 attempt <= MaxRecoveryAttempts;
                 attempt++)
            {
                ct.ThrowIfCancellationRequested();

                await StopProcessCoreAsync(
                    completeConsumers: true);

                if (attempt > 1)
                {
                    var delay =
                        TimeSpan.FromSeconds(
                            Math.Pow(
                                2,
                                attempt - 2));

                    await Task.Delay(
                        delay,
                        ct);
                }

                try
                {
                    await StartCoreAsync(
                        ct,
                        preserveRecoveringState: true);

                    return;
                }
                catch (Exception ex)
                    when (ex is not
                        OperationCanceledException)
                {
                    lastError = ex;

                    _logger.LogWarning(
                        "Canonical media recovery attempt {Attempt}/{Max} failed ({ErrorType}).",
                        attempt,
                        MaxRecoveryAttempts,
                        ex.GetType().Name);
                }
            }

            SetState(
                StreamReceiverState.Error);

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
            SetState(
                StreamReceiverState.Starting);
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
            BuildReceiverArguments(
                runtimeRtspUrl);

        _lifetimeCts =
            CancellationTokenSource
                .CreateLinkedTokenSource(ct);

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
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        _mediaTask =
            PumpMediaAsync(
                process,
                firstMedia,
                _lifetimeCts.Token);

        _diagnosticsTask =
            PumpDiagnosticsAsync(
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

        SetState(
            StreamReceiverState.Available);
    }

    private async Task PumpMediaAsync(
        Process process,
        TaskCompletionSource<bool> firstMedia,
        CancellationToken ct)
    {
        var buffer =
            new byte[MediaBufferSize];

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var read =
                    await process.StandardOutput
                        .BaseStream
                        .ReadAsync(
                            buffer.AsMemory(
                                0,
                                buffer.Length),
                            ct);

                if (read == 0)
                {
                    return;
                }

                firstMedia.TrySetResult(true);

                var chunk =
                    buffer.AsSpan(
                            0,
                            read)
                        .ToArray();
                byte[] alignedMedia;
                KeyValuePair<Guid, MediaConsumer>[] consumers;

                lock (_mediaDispatchLock)
                {
                    alignedMedia =
                        _bootstrap.Append(
                            chunk);

                    if (alignedMedia.Length == 0)
                    {
                        continue;
                    }

                    consumers =
                        _consumers.ToArray();
                }

                foreach (var entry in consumers)
                {
                    var consumer = entry.Value;

                    if (consumer.LiveBootstrap is not null)
                    {
                        consumer.LiveBootstrap.Append(
                            alignedMedia);

                        var liveBootstrap =
                            consumer.LiveBootstrap.Snapshot();

                        if (liveBootstrap.Length == 0)
                        {
                            continue;
                        }

                        var delivered = false;

                        if (consumer.LossIntolerant)
                        {
                            try
                            {
                                await consumer.Channel.Writer.WriteAsync(
                                    liveBootstrap,
                                    ct);
                                delivered = true;
                            }
                            catch (ChannelClosedException)
                                when (!_consumers.ContainsKey(entry.Key))
                            {
                                // The lease was detached after the dispatch snapshot.
                            }
                        }
                        else
                        {
                            delivered =
                                consumer.Channel.Writer.TryWrite(
                                    liveBootstrap);
                        }

                        if (!delivered)
                        {
                            continue;
                        }

                        consumer.LiveBootstrap = null;
                        consumer.LiveReady.TrySetResult(true);
                        continue;
                    }

                    if (consumer.LossIntolerant)
                    {
                        try
                        {
                            await consumer.Channel.Writer.WriteAsync(
                                alignedMedia,
                                ct);
                        }
                        catch (ChannelClosedException)
                            when (!_consumers.ContainsKey(entry.Key))
                        {
                            // The lease was detached after the dispatch snapshot.
                        }
                    }
                    else
                    {
                        consumer.Channel.Writer.TryWrite(
                            alignedMedia);
                    }
                }
            }
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            // Expected during StopAsync or recovery.
        }
        catch (Exception ex)
        {
            firstMedia.TrySetException(
                new InvalidOperationException(
                    "Canonical media distribution failed.",
                    ex));
        }
    }

    private async Task PumpDiagnosticsAsync(
        Process process,
        CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line =
                    await process.StandardError
                        .ReadLineAsync(ct);

                if (line is null)
                {
                    return;
                }

                if (TryApplyProgressLine(
                        line))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(
                        line))
                {
                    _logger.LogDebug(
                        "Canonical media receiver FFmpeg: {Diagnostic}",
                        RedactRtspCredentials(
                            line));
                }
            }
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            // Expected during StopAsync or recovery.
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

        if (State is
            StreamReceiverState.Starting or
            StreamReceiverState.Available)
        {
            SetState(
                StreamReceiverState.Interrupted);

            CompleteAllConsumers();

            _logger.LogWarning(
                "Canonical live-video media receiver was interrupted; FFmpeg exit code={ExitCode}.",
                process.ExitCode);
        }
    }

    private bool TryApplyProgressLine(
        string line)
    {
        if (string.IsNullOrWhiteSpace(
                line))
        {
            return false;
        }

        var separator =
            line.IndexOf('=');

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
        var droppedFrames =
            health.DroppedFrames;
        var recognised = true;

        switch (key)
        {
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
                    droppedFrames =
                        dropped;
                }
                break;

            case "frame":
            case "out_time_us":
            case "out_time_ms":
            case "out_time":
            case "speed":
            case "dup_frames":
            case "total_size":
            case "progress":
                break;

            default:
                recognised = false;
                break;
        }

        if (recognised)
        {
            UpdateHealth(
                bitRate,
                frameRate,
                droppedFrames);
        }

        return recognised;
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
                    Timestamp =
                        DateTimeOffset.UtcNow
                };

            _health = health;
        }

        StateChanged?.Invoke(state);
        HealthChanged?.Invoke(health);
    }

    private async Task StopProcessCoreAsync(
        bool completeConsumers)
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

                        await process
                            .WaitForExitAsync(
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
                    _mediaTask,
                    _diagnosticsTask,
                    _exitTask
                }
                .Where(task =>
                    task is not null)
                .Cast<Task>()
                .ToArray();

            if (tasks.Length > 0)
            {
                try
                {
                    await Task.WhenAll(
                        tasks);
                }
                catch (OperationCanceledException)
                {
                    // Expected during process teardown.
                }
                catch
                {
                    // Teardown remains best-effort.
                }
            }
        }
        finally
        {
            _mediaTask = null;
            _diagnosticsTask = null;
            _exitTask = null;

            process?.Dispose();
            lifetimeCts?.Dispose();


            lock (_mediaDispatchLock)
            {
                _bootstrap.Reset();
            }

            if (completeConsumers)
            {
                CompleteAllConsumers();
            }
        }
    }

    private void RemoveConsumer(
        Guid consumerId)
    {
        if (_consumers.TryRemove(
                consumerId,
                out var consumer))
        {
            consumer.Channel.Writer.TryComplete();
        }
    }

    private void CompleteAllConsumers()
    {
        foreach (var consumer
            in _consumers.ToArray())
        {
            if (_consumers.TryRemove(
                    consumer.Key,
                    out var registeredConsumer))
            {
                registeredConsumer.Channel.Writer
                    .TryComplete();
            }
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
            "-progress pipe:2",
            "-nostats",
            "-rtsp_transport tcp",
            "-fflags nobuffer",
            "-flags low_delay",
            "-i",
            $"\"{authenticatedRtspUrl}\"",
            "-map 0:v:0",
            "-an",
            "-c:v copy",
            "-f mpegts",
            "pipe:1");
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

    private sealed class MpegTsBootstrapBuffer
    {
        private const int PacketSize = 188;
        private const int MaxPackets = 16384;
        private const int MaxBootstrapPackets = 8192;

        private readonly List<PacketEntry> _packets = new();
        private byte[] _pending = Array.Empty<byte>();
        private bool _isSynchronized;
        private int? _pmtPid;
        private int? _videoPid;

        public byte[] Append(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var combined = new byte[_pending.Length + bytes.Length];
            Buffer.BlockCopy(_pending, 0, combined, 0, _pending.Length);
            Buffer.BlockCopy(bytes, 0, combined, _pending.Length, bytes.Length);

            var offset = 0;

            if (!_isSynchronized)
            {
                offset = FindPacketAlignment(combined);

                if (offset < 0)
                {
                    _pending = combined.Length > PacketSize * 3
                        ? combined[^(PacketSize * 3)..]
                        : combined;
                    return Array.Empty<byte>();
                }

                _isSynchronized = true;
            }

            var alignedPackets = new List<byte[]>();

            while (offset + PacketSize <= combined.Length)
            {
                if (combined[offset] != 0x47)
                {
                    _isSynchronized = false;

                    var next = FindPacketAlignment(combined, offset + 1);
                    if (next < 0)
                    {
                        break;
                    }

                    offset = next;
                    _isSynchronized = true;
                    continue;
                }

                var packet = new byte[PacketSize];
                Buffer.BlockCopy(combined, offset, packet, 0, PacketSize);
                AppendPacket(packet);
                alignedPackets.Add(packet);
                offset += PacketSize;
            }

            var remaining = combined.Length - offset;
            if (remaining > 0)
            {
                _pending = new byte[remaining];
                Buffer.BlockCopy(combined, offset, _pending, 0, remaining);
            }
            else
            {
                _pending = Array.Empty<byte>();
            }

            if (alignedPackets.Count == 0)
            {
                return Array.Empty<byte>();
            }

            var aligned = new byte[alignedPackets.Count * PacketSize];
            var destinationOffset = 0;
            foreach (var packet in alignedPackets)
            {
                Buffer.BlockCopy(packet, 0, aligned, destinationOffset, PacketSize);
                destinationOffset += PacketSize;
            }

            return aligned;
        }

        public byte[] Snapshot()
        {
            if (_packets.Count == 0) { return Array.Empty<byte>(); }

            var idrIndex = -1;
            for (var i = _packets.Count - 1; i >= 0; i--)
            {
                if (_packets[i].HasIdr)
                {
                    idrIndex = i;
                    break;
                }
            }

            if (idrIndex < 0) { return Array.Empty<byte>(); }

            var startIndex = -1;
            for (var i = idrIndex; i >= 0 && idrIndex - i < MaxBootstrapPackets; i--)
            {
                if (_packets[i].IsPat)
                {
                    startIndex = i;
                    break;
                }
            }

            if (startIndex < 0) { return Array.Empty<byte>(); }

            var hasPmt = false;
            for (var i = startIndex; i <= idrIndex; i++)
            {
                if (_packets[i].IsPmt)
                {
                    hasPmt = true;
                    break;
                }
            }

            if (!hasPmt) { return Array.Empty<byte>(); }

            var packetCount = _packets.Count - startIndex;
            if (packetCount > MaxBootstrapPackets) { return Array.Empty<byte>(); }

            var snapshot = new byte[packetCount * PacketSize];
            var destinationOffset = 0;

            for (var i = startIndex; i < _packets.Count; i++)
            {
                Buffer.BlockCopy(_packets[i].Bytes, 0, snapshot, destinationOffset, PacketSize);
                destinationOffset += PacketSize;
            }

            return snapshot;
        }

        public void Reset()
        {
            _packets.Clear();
            _pending = Array.Empty<byte>();
            _isSynchronized = false;
            _pmtPid = null;
            _videoPid = null;
        }

        private void AppendPacket(byte[] packet)
        {
            var pid = ((packet[1] & 0x1F) << 8) | packet[2];
            var payloadOffset = GetPayloadOffset(packet);

            var isPat = pid == 0 && payloadOffset >= 0;
            var isPmt = _pmtPid.HasValue && pid == _pmtPid.Value && payloadOffset >= 0;

            if (isPat)
            {
                var parsedPmtPid = TryParsePmtPid(packet, payloadOffset);
                if (parsedPmtPid.HasValue) { _pmtPid = parsedPmtPid.Value; }
            }

            if (isPmt)
            {
                var parsedVideoPid = TryParseVideoPid(packet, payloadOffset);
                if (parsedVideoPid.HasValue) { _videoPid = parsedVideoPid.Value; }
            }

            var hasIdr =
                _videoPid.HasValue &&
                pid == _videoPid.Value &&
                payloadOffset >= 0 &&
                ContainsH264Idr(packet, payloadOffset);

            _packets.Add(new PacketEntry(packet, isPat, isPmt, hasIdr));

            while (_packets.Count > MaxPackets)
            {
                _packets.RemoveAt(0);
            }
        }

        private static int FindPacketAlignment(byte[] bytes, int start = 0)
        {
            var limit = bytes.Length - (PacketSize * 2);
            for (var i = start; i < limit; i++)
            {
                if (bytes[i] == 0x47 &&
                    bytes[i + PacketSize] == 0x47 &&
                    bytes[i + (PacketSize * 2)] == 0x47)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int GetPayloadOffset(byte[] packet)
        {
            var adaptationControl = (packet[3] >> 4) & 0x03;
            if (adaptationControl == 0 || adaptationControl == 2) { return -1; }

            var offset = 4;
            if (adaptationControl == 3)
            {
                var adaptationLength = packet[offset];
                offset += adaptationLength + 1;
            }

            return offset < PacketSize ? offset : -1;
        }

        private static int? TryParsePmtPid(byte[] packet, int payloadOffset)
        {
            var offset = SkipPointerField(packet, payloadOffset);
            if (offset < 0 || offset + 12 >= PacketSize || packet[offset] != 0x00) { return null; }

            return ((packet[offset + 10] & 0x1F) << 8) | packet[offset + 11];
        }

        private static int? TryParseVideoPid(byte[] packet, int payloadOffset)
        {
            var offset = SkipPointerField(packet, payloadOffset);
            if (offset < 0 || offset + 12 >= PacketSize || packet[offset] != 0x02) { return null; }

            var sectionLength = ((packet[offset + 1] & 0x0F) << 8) | packet[offset + 2];
            var sectionEnd = Math.Min(PacketSize, offset + 3 + sectionLength);
            var programInfoLength = ((packet[offset + 10] & 0x0F) << 8) | packet[offset + 11];
            var streamOffset = offset + 12 + programInfoLength;

            while (streamOffset + 5 <= sectionEnd - 4)
            {
                var streamType = packet[streamOffset];
                var elementaryPid = ((packet[streamOffset + 1] & 0x1F) << 8) | packet[streamOffset + 2];
                var esInfoLength = ((packet[streamOffset + 3] & 0x0F) << 8) | packet[streamOffset + 4];

                if (streamType == 0x1B || streamType == 0x24)
                {
                    return elementaryPid;
                }

                streamOffset += 5 + esInfoLength;
            }

            return null;
        }

        private static int SkipPointerField(byte[] packet, int payloadOffset)
        {
            var payloadUnitStart = (packet[1] & 0x40) != 0;
            if (!payloadUnitStart) { return payloadOffset; }
            if (payloadOffset >= PacketSize) { return -1; }

            var pointer = packet[payloadOffset];
            var offset = payloadOffset + 1 + pointer;
            return offset < PacketSize ? offset : -1;
        }

        private static bool ContainsH264Idr(byte[] packet, int payloadOffset)
        {
            for (var i = payloadOffset; i + 4 < PacketSize; i++)
            {
                var nalOffset = -1;

                if (packet[i] == 0x00 && packet[i + 1] == 0x00 && packet[i + 2] == 0x01)
                {
                    nalOffset = i + 3;
                }
                else if (i + 5 < PacketSize &&
                         packet[i] == 0x00 && packet[i + 1] == 0x00 &&
                         packet[i + 2] == 0x00 && packet[i + 3] == 0x01)
                {
                    nalOffset = i + 4;
                }

                if (nalOffset >= 0 && nalOffset < PacketSize)
                {
                    var nalType = packet[nalOffset] & 0x1F;
                    if (nalType == 5) { return true; }
                }
            }

            return false;
        }

        private sealed record PacketEntry(
            byte[] Bytes,
            bool IsPat,
            bool IsPmt,
            bool HasIdr);
    }
    private sealed class MediaConsumer
    {
        public MediaConsumer(
            Channel<byte[]> channel,
            bool lossIntolerant,
            MpegTsBootstrapBuffer? liveBootstrap)
        {
            Channel = channel;
            LossIntolerant = lossIntolerant;
            LiveBootstrap = liveBootstrap;
        }

        public Channel<byte[]> Channel { get; }

        public bool LossIntolerant { get; }

        public MpegTsBootstrapBuffer? LiveBootstrap { get; set; }

        public TaskCompletionSource<bool> LiveReady { get; } =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
    }

    private sealed class StreamReceiverMediaLease :
        IStreamReceiverMediaLease
    {
        private readonly Action _release;
        private int _disposed;

        public StreamReceiverMediaLease(
            Stream stream,
            Action release)
        {
            Stream = stream;
            _release = release;
        }

        public Stream Stream { get; }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(
                    ref _disposed,
                    1) == 0)
            {
                _release();
                Stream.Dispose();
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ChannelReadStream :
        Stream
    {
        private readonly ChannelReader<byte[]> _reader;
        private byte[]? _current;
        private int _offset;

        public ChannelReadStream(
            ChannelReader<byte[]> reader)
        {
            _reader = reader;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get =>
                throw new NotSupportedException();
            set =>
                throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count) =>
            ReadAsync(
                    buffer,
                    offset,
                    count,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            return await ReadAsync(
                buffer.AsMemory(
                    offset,
                    count),
                cancellationToken);
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            while (_current is null ||
                   _offset >= _current.Length)
            {
                if (!await _reader
                        .WaitToReadAsync(
                            cancellationToken))
                {
                    return 0;
                }

                if (!_reader.TryRead(
                        out _current))
                {
                    continue;
                }

                _offset = 0;
            }

            var available =
                _current.Length -
                _offset;

            var toCopy =
                Math.Min(
                    available,
                    buffer.Length);

            _current.AsMemory(
                    _offset,
                    toCopy)
                .CopyTo(buffer);

            _offset += toCopy;

            return toCopy;
        }

        public override long Seek(
            long offset,
            SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(
            long value) =>
            throw new NotSupportedException();

        public override void Write(
            byte[] buffer,
            int offset,
            int count) =>
            throw new NotSupportedException();
    }
}
