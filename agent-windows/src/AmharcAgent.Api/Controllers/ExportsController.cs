using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Domain;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api/matches/{matchId}/export")]
public class ExportsController(
    IExportService exports,
    AmharcAgent.Core.Domain.AgentSettings settings,
    ILogger<ExportsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> ExportMatch(string matchId, CancellationToken ct)
    {
        try
        {
            var outputDir = Path.Combine(settings.RecordingDirectory, matchId, "export");
            Directory.CreateDirectory(outputDir);

            var eventsJson = Path.Combine(outputDir, $"{matchId}_events.json");
            var eventsCsv = Path.Combine(outputDir, $"{matchId}_events.csv");
            var manifestPath = Path.Combine(outputDir, $"{matchId}_manifest.json");
            var technicalLog = Path.Combine(outputDir, $"{matchId}_technical.log");

            await Task.WhenAll(
                File.WriteAllTextAsync(eventsJson, await exports.ExportEventsJsonAsync(matchId, ct), ct),
                File.WriteAllTextAsync(eventsCsv, await exports.ExportEventsCsvAsync(matchId, ct), ct),
                exports.WriteManifestAsync(matchId, manifestPath, ct),
                exports.WriteTechnicalLogAsync(matchId, technicalLog, ct));

            logger.LogInformation("Export complete for match {MatchId} → {Dir}", matchId, outputDir);

            return Ok(new
            {
                outputDirectory = outputDir,
                files = new
                {
                    eventsJson = eventsJson,
                    eventsCsv = eventsCsv,
                    manifest = manifestPath,
                    technicalLog = technicalLog
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Export failed for match {MatchId}", matchId);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
