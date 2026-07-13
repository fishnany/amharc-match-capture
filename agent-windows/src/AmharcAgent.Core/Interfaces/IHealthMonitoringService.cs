using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

/// <summary>Aggregates health state from all subsystems.</summary>
public interface IHealthMonitoringService
{
    SystemHealth Health { get; }
    SystemHealth GetHealth();
    event Action<string, string> Warning;
    event Action<string, string> ErrorOccurred;
}
