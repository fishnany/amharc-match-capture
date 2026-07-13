using System.Diagnostics;
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
    private readonly string _ffmpegPath;
    private Process? _ffmpegProcess;
    private readonly Stopwatch _elapsedStopwatch = new();
    private RecordingState _state = RecordingState.Idle;
    private RecordingOptions? _currentOptions;
    private readonly Lock _lock = new();

    public event Action<RecordingState>? StateChanged;

    public FfmpegRecordingService(ILogger<FfmpegRecordingService> logger, string ffmpegPath = "ffmpeg.exe")
    {
        _logger = logger;
        _ffmpegPath = ffmpegPath;
    }

    public RecordingState State => _state;
    public double ElapsedSeconds => _elapsedStopwatch.Elapsed.TotalSeconds;
    public int SegmentCount => GetSegments().Count;
    public string? OutputDirectory => _currentOptions?.OutputDirectory;

    public async Task StartRecordingAsync(RecordingOptions options, CancellationToken ct = default)
    {
        if (_state == RecordingState.Recording)
            throw new InvalidOperationException("Already recording.");

        SetState(RecordingState.Starting);
        _currentOptions = options;
        Directory.CreateDirectory(options.OutputDirectory);

        var outputPattern = Path.Combine(options.OutputDirectory,
            $"{options.MatchId}_%Y%m%d_%H%M%S_%03d.mkv");

        var audioArgs = options.IncludeAudio ? "-c:a copy" : "-an";
        var args = string.Join(" ",
            "-rtsp_transport tcp",
            $"-i \"{options.RtspUrl}\"",
            "-c:v copy",
            audioArgs,
            "-f segment",
            $"-segment_time {options.SegmentDurationSeconds}",
            "-segment_format mkv",
            "-reset_timestamps 1",
            "-strftime 1",
            $"\"{outputPattern}\"");

        _logger.LogInformation("Starting FFmpeg recording: {Args}", args);

        _ffmpegProcess = new Process
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
            EnableRaisingEvents = true
        };
        _ffmpegProcess.ErrorDataReceived += OnFfmpegStderr;
        _ffmpegProcess.Exited += OnFfmpegExited;
        _ffmpegProcess.Start();
        _ffmpegProcess.BeginErrorReadLine();
        _elapsedStopwatch.Restart();

        SetState(RecordingState.Recording);
        _logger.LogInformation("Recording started for match {MatchId}", options.MatchId);
        await Task.CompletedTask;
    }

    public async Task StopRecordingAsync(CancellationToken ct = default)
    {
        if (_ffmpegProcess is null || _state != RecordingState.Recording) return;
        SetState(RecordingState.Stopping);

        try
        {
            // Send 'q' to FFmpeg stdin for a clean segment-boundary shutdown
            await _ffmpegProcess.StandardInput.WriteAsync('q');
            await _ffmpegProcess.StandardInput.FlushAsync(ct);

            if (!_ffmpegProcess.WaitForExit(10_000))
            {
                _logger.LogWarning("FFmpeg did not exit cleanly — killing process");
                _ffmpegProcess.Kill();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping FFmpeg");
        }
        finally
        {
            _elapsedStopwatch.Stop();
            _ffmpegProcess = null;
            SetState(RecordingState.Complete);
            _logger.LogInformation("Recording stopped. Elapsed: {Seconds:F1}s", ElapsedSeconds);
        }
    }

    public async Task<string> RemuxToMp4Async(CancellationToken ct = default)
    {
        if (_currentOptions is null) throw new InvalidOperationException("No recording session active.");
        SetState(RecordingState.Remuxing);

        var segments = GetSegments().Select(s => s.FilePath).ToList();
        if (segments.Count == 0) throw new InvalidOperationException("No segments to remux.");

        var concatFile = Path.Combine(_currentOptions.OutputDirectory, "concat.txt");
        await File.WriteAllLinesAsync(concatFile,
            segments.Select(s => $"file '{s.Replace("'", "'\\''")}'"), ct);

        var outputPath = Path.Combine(_currentOptions.OutputDirectory,
            $"{_currentOptions.MatchId}_final.mp4");

        var args = $"-f concat -safe 0 -i \"{concatFile}\" -c copy \"{outputPath}\"";
        _logger.LogInformation("Remuxing to MP4: {Output}", outputPath);

        using var proc = Process.Start(new ProcessStartInfo
        {
            FileName = _ffmpegPath, Arguments = args,
            UseShellExecute = false, CreateNoWindow = true
        });
        await proc!.WaitForExitAsync(ct);
        File.Delete(concatFile);

        SetState(RecordingState.Complete);
        _logger.LogInformation("Remux complete: {Path}", outputPath);
        return outputPath;
    }

    public Task RecoverAsync(CancellationToken ct = default)
    {
        SetState(RecordingState.Recovering);
        var segments = GetSegments();
        _logger.LogInformation("Recovered {Count} segment(s)", segments.Count);
        SetState(segments.Count > 0 ? RecordingState.Complete : RecordingState.Error);
        return Task.CompletedTask;
    }

    public IReadOnlyList<RecordingSegmentInfo> GetSegments()
    {
        if (_currentOptions is null) return [];
        if (!Directory.Exists(_currentOptions.OutputDirectory)) return [];

        return Directory
            .GetFiles(_currentOptions.OutputDirectory, $"{_currentOptions.MatchId}_*.mkv")
            .OrderBy(f => f)
            .Select((f, i) =>
            {
                var info = new FileInfo(f);
                return new RecordingSegmentInfo(
                    i + 1, f, info.CreationTimeUtc, info.LastWriteTimeUtc, true, null, info.Length);
            })
            .ToList();
    }

    public async Task<string> GetChecksumAsync(string filePath, CancellationToken ct = default)
    {
        using var stream = File.OpenRead(filePath);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private void OnFfmpegStderr(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is null) return;
        if (e.Data.Contains("Error") || e.Data.Contains("error"))
            _logger.LogWarning("FFmpeg: {Line}", e.Data);
        else
            _logger.LogTrace("FFmpeg: {Line}", e.Data);
    }

    private void OnFfmpegExited(object? sender, EventArgs e)
    {
        if (_state == RecordingState.Recording)
        {
            _logger.LogWarning("FFmpeg exited unexpectedly");
            SetState(RecordingState.Error);
        }
    }

    private void SetState(RecordingState state)
    {
        lock (_lock) { _state = state; }
        StateChanged?.Invoke(state);
    }

    public async ValueTask DisposeAsync()
    {
        if (_ffmpegProcess is { HasExited: false })
            await StopRecordingAsync();
    }
}
