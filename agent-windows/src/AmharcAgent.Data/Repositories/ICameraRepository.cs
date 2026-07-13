using AmharcAgent.Core.Domain;

namespace AmharcAgent.Data.Repositories;

/// <summary>Repository contract for <see cref="Camera"/> persistence.</summary>
public interface ICameraRepository
{
    /// <summary>Returns all configured cameras.</summary>
    Task<IEnumerable<Camera>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the camera with the specified identifier, or null if not found.</summary>
    Task<Camera?> GetByIdAsync(string cameraId, CancellationToken cancellationToken = default);

    /// <summary>Adds a new camera to the store.</summary>
    Task AddAsync(Camera camera, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing camera.</summary>
    Task UpdateAsync(Camera camera, CancellationToken cancellationToken = default);

    /// <summary>Removes the camera with the specified identifier.</summary>
    Task DeleteAsync(string cameraId, CancellationToken cancellationToken = default);
}
