using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmharcAgent.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Recording;

/// <summary>
/// Scans a directory for MKV segment files and provides helpers for
/// checksums and FFmpeg concat file generation.
/// </summary>
public sealed class SegmentManager
{
    private readonly ILogger<SegmentManager> _logger;

    /// <summary>Initialises the segment manager.</summary>
    public SegmentManager(ILogger<SegmentManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Scans <paramref name="directory"/> for MKV files matching
    /// <c>{matchId}_*.mkv</c> and returns them ordered by filename.
    /// </summary>
    public IReadOnlyList<RecordingSegmentInfo> GetSegments(string directory, string matchId)
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Segment directory does not exist: {Dir}", directory);
            return Array.Empty<RecordingSegmentInfo>();
        }

        var pattern = $"{matchId}_*.mkv";
        var files = Directory.GetFiles(directory, pattern)
            .OrderBy(f => f)
            .ToList();

        var segments = new List<RecordingSegmentInfo>();
        for (int i = 0; i < files.Count; i++)
        {
            var path = files[i];
            var fileInfo = new FileInfo(path);
            var startTs = ParseTimestampFromFileName(path) ??
                          new DateTimeOffset(fileInfo.CreationTimeUtc, TimeSpan.Zero);

            DateTimeOffset? endTs = i + 1 < files.Count
                ? ParseTimestampFromFileName(files[i + 1]) ??
                  new DateTimeOffset(new FileInfo(files[i + 1]).CreationTimeUtc, TimeSpan.Zero)
                : null;

            double? duration = endTs.HasValue ? (endTs.Value - startTs).TotalSeconds : null;

            segments.Add(new RecordingSegmentInfo
            {
                SegmentNumber = i + 1,
                FilePath = path,
                StartTimestamp = startTs,
                EndTimestamp = endTs,
                IsComplete = endTs.HasValue,
                DurationSeconds = duration,
                FileSizeBytes = fileInfo.Exists ? fileInfo.Length : null
            });
        }

        _logger.LogDebug("Found {Count} segments in {Dir} for match {MatchId}",
            segments.Count, directory, matchId);
        return segments;
    }

    /// <summary>
    /// Computes the SHA-256 checksum of a file and returns it as a lowercase hex string.
    /// </summary>
    public async Task<string> GetChecksumAsync(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Writes an FFmpeg concat filelist to <paramref name="outputPath"/>.
    /// </summary>
    public void WriteConcatFile(IReadOnlyList<RecordingSegmentInfo> segments, string outputPath)
    {
        var sb = new StringBuilder();
        foreach (var seg in segments)
        {
            var safePath = seg.FilePath.Replace("'", "'\\''");
            sb.AppendLine($"file '{safePath}'");
        }
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        _logger.LogDebug("Written concat file with {Count} entries to {Path}",
            segments.Count, outputPath);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Parses a timestamp from a filename of the form
    /// <c>{matchId}_YYYYMMDD_HHmmss.mkv</c>.
    /// Returns null if parsing fails.
    /// </summary>
    private static DateTimeOffset? ParseTimestampFromFileName(string filePath)
    {
        try
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            var parts = name.Split('_');
            if (parts.Length < 3) return null;
            var datePart = parts[^2];
            var timePart = parts[^1];
            if (datePart.Length == 8 && timePart.Length == 6 &&
                DateTime.TryParseExact(
                    $"{datePart}{timePart}",
                    "yyyyMMddHHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var dt))
            {
                return new DateTimeOffset(dt, TimeSpan.Zero);
            }
        }
        catch { /* ignore */ }
        return null;
    }
}
