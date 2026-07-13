using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

/// <summary>Controls FFmpeg-based MKV segment recording.</summary>
public interface IRecordingService
{
    RecordingState State { get; }
    double ElapsedSeconds { get; }
    int SegmentCount { get; }
    string? OutputDirectory { get; }

    Task StartRecordingAsync(RecordingOptions options, CancellationToken ct = default);
    Task StopRecordingAsync(CancellationToken ct = default);
    Task<string> RemuxToMp4Async(CancellationToken ct = default);
    Task RecoverAsync(CancellationToken ct = default);
    IReadOnlyList<RecordingSegmentInfo> GetSegments();
    Task<string> GetChecksumAsync(string filePath, CancellationToken ct = default);

    event Action<RecordingState> StateChanged;
}
