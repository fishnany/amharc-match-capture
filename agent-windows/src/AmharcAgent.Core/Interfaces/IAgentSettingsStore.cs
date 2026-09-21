using AmharcAgent.Core.Domain;

namespace AmharcAgent.Core.Interfaces;

public interface IAgentSettingsStore
{
    Task SaveAsync(
        AgentSettings settings,
        CancellationToken cancellationToken = default);
}