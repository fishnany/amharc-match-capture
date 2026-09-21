using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

public interface IProtectedCredentialStore
{
    ValueTask<ProtectedCredential?> ReadAsync(string targetName, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(string targetName, ProtectedCredential credential, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(string targetName, CancellationToken cancellationToken = default);
}
