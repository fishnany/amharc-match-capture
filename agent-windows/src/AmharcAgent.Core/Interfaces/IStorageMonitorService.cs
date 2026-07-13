using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

/// <summary>Monitors recording drive space and warns when low.</summary>
public interface IStorageMonitorService
{
    StorageStatus Status { get; }
    Task<StorageStatus> CheckAsync(CancellationToken ct = default);
    bool HasMinimumSpace();
    event Action<StorageStatus> Warning;
}
