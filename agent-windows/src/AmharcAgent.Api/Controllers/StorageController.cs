using AmharcAgent.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AmharcAgent.Api.Controllers;

[ApiController]
[Route("api/storage")]
public class StorageController(IStorageMonitorService storage) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct) =>
        Ok(await storage.CheckAsync(ct));
}
