using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Interfaces;

/// <summary>Produces export files for a completed match.</summary>
public interface IExportService
{
    Task<string> ExportEventsJsonAsync(string matchId, CancellationToken ct = default);
    Task<string> ExportEventsCsvAsync(string matchId, CancellationToken ct = default);
    Task<ExportManifest> GenerateManifestAsync(string matchId, CancellationToken ct = default);
    Task WriteManifestAsync(string matchId, string outputPath, CancellationToken ct = default);
    Task WriteTechnicalLogAsync(string matchId, string outputPath, CancellationToken ct = default);
}
