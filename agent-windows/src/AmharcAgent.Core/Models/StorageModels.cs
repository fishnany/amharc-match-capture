namespace AmharcAgent.Core.Models;

/// <summary>Storage warning threshold level.</summary>
public enum StorageWarningLevel { Ok, Warning, Critical }

/// <summary>Snapshot of current storage status.</summary>
public record StorageStatus(
    long TotalBytes,
    long AvailableBytes,
    double AvailableMinutes,
    string RecordingDirectory,
    StorageWarningLevel WarningLevel,
    bool IsExternalStorage);
