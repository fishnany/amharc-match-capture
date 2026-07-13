using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace AmharcAgent.Infrastructure.Storage;

/// <summary>
/// Monitors available disk space on the recording drive.
/// Available minutes are estimated at 50 Mbps recording bitrate.
/// </summary>
public class StorageMonitorService : IStorageMonitorService, IDisposable
{
    private readonly ILogger<StorageMonitorService> _logger;
    private readonly string _recordingDirectory;
    private StorageStatus _status;
    private readonly System.Threading.Timer _timer;

    // Assume ~50 Mbps average bitrate for H.264 4K recording
    private const double BitrateBytesPerSecond = 50_000_000.0 / 8.0;

    public StorageStatus Status => _status;
    public event Action<StorageStatus>? Warning;

    public StorageMonitorService(ILogger<StorageMonitorService> logger, string recordingDirectory)
    {
        _logger = logger;
        _recordingDirectory = recordingDirectory;
        _status = BuildStatus(recordingDirectory);
        _timer = new System.Threading.Timer(
            _ => { var prev = _status; CheckAndFire(prev); },
            null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public async Task<StorageStatus> CheckAsync(CancellationToken ct = default)
    {
        _status = BuildStatus(_recordingDirectory);
        return await Task.FromResult(_status);
    }

    public bool HasMinimumSpace() => _status.AvailableMinutes >= 15;

    private void CheckAndFire(StorageStatus previous)
    {
        _status = BuildStatus(_recordingDirectory);
        if (_status.WarningLevel != StorageWarningLevel.Ok)
        {
            _logger.LogWarning("Storage warning: {Minutes:F0} min available ({Level})",
                _status.AvailableMinutes, _status.WarningLevel);
            Warning?.Invoke(_status);
        }
    }

    private static StorageStatus BuildStatus(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var root = Path.GetPathRoot(directory) ?? directory;
            var drive = new DriveInfo(root);
            var available = drive.AvailableFreeSpace;
            var total = drive.TotalSize;
            var used = total - available;
            var minutes = available / BitrateBytesPerSecond / 60.0;
            var level = minutes switch
            {
                >= 60 => StorageWarningLevel.Ok,
                >= 15 => StorageWarningLevel.Warning,
                _ => StorageWarningLevel.Critical
            };
            return new StorageStatus(total, used, available, minutes, directory, level,
                drive.DriveType == DriveType.Removable || drive.DriveType == DriveType.Network);
        }
        catch
        {
            return new StorageStatus(0, 0, 0, 0, directory, StorageWarningLevel.Critical, false);
        }
    }

    public void Dispose() => _timer.Dispose();
}
