using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

public interface IAmharcCommandDispatcher
{
    Task DispatchAsync(
        AmharcCommand command,
        CancellationToken ct = default);
}
