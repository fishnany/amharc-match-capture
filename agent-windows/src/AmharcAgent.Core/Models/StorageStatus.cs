namespace AmharcAgent.Core.Models;

public enum StorageWarningLevel { Ok, Warning, Critical }

public record StorageStatus(
    long TotalBytes,
    long UsedBytes,
    long AvailableBytes,
    double AvailableMinutes,
    string RecordingDirectory,
    StorageWarningLevel WarningLevel,
    bool IsExternalStorage);
