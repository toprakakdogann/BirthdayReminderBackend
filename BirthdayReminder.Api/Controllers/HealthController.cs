using BirthdayReminder.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BirthdayReminder.Api.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public async Task<IActionResult> Health([FromServices] AppDbContext db)
    {
        // DB bağlantısı canlı mı? (çok hafif)
        var canConnect = await db.Database.CanConnectAsync();
        if (!canConnect)
            return StatusCode(503, new { status = "unhealthy", db = "down" });

        return Ok(new { status = "ok", db = "up" });
    }
}
