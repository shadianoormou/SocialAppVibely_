using InstagramClone.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstagramClone.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public HealthController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<object>> Get()
    {
        var databaseReady = await _dbContext.Database.CanConnectAsync();
        return databaseReady
            ? Ok(new { status = "ok", database = "ok", utc = DateTime.UtcNow })
            : StatusCode(503, new { status = "degraded", database = "unavailable", utc = DateTime.UtcNow });
    }
}
