using AmharcAgent.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace AmharcAgent.Data.Repositories;

public interface ICameraRepository
{
    Task<IReadOnlyList<Camera>> GetAllAsync(CancellationToken ct = default);
    Task<Camera?> GetByIdAsync(string cameraId, CancellationToken ct = default);
    Task<Camera> CreateAsync(Camera camera, CancellationToken ct = default);
    Task<Camera> UpdateAsync(Camera camera, CancellationToken ct = default);
    Task DeleteAsync(string cameraId, CancellationToken ct = default);
}

public class CameraRepository(AmharcDbContext db) : ICameraRepository
{
    public async Task<IReadOnlyList<Camera>> GetAllAsync(CancellationToken ct = default) =>
        await db.Cameras.OrderBy(c => c.Name).ToListAsync(ct);

    public async Task<Camera?> GetByIdAsync(string cameraId, CancellationToken ct = default) =>
        await db.Cameras.FindAsync([cameraId], ct);

    public async Task<Camera> CreateAsync(Camera camera, CancellationToken ct = default)
    {
        db.Cameras.Add(camera);
        await db.SaveChangesAsync(ct);
        return camera;
    }

    public async Task<Camera> UpdateAsync(Camera camera, CancellationToken ct = default)
    {
        db.Cameras.Update(camera);
        await db.SaveChangesAsync(ct);
        return camera;
    }

    public async Task DeleteAsync(string cameraId, CancellationToken ct = default)
    {
        var camera = await GetByIdAsync(cameraId, ct);
        if (camera is not null)
        {
            db.Cameras.Remove(camera);
            await db.SaveChangesAsync(ct);
        }
    }
}
