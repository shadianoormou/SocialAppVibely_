using System.Security.Claims;
using InstagramClone.Api.Data;
using InstagramClone.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstagramClone.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public NotificationsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> Get([FromQuery] int take = 30)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        take = Math.Clamp(take, 1, 100);
        var notifications = await _dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.RecipientId == userId)
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Take(take)
            .Select(notification => new NotificationResponse
            {
                Id = notification.Id,
                Type = notification.Type,
                ActorUserName = notification.Actor.UserName,
                ActorProfileImageUrl = notification.Actor.ProfileImageUrl,
                PostId = notification.PostId,
                Message = notification.Message,
                CreatedAtUtc = notification.CreatedAtUtc,
                IsRead = notification.ReadAtUtc != null
            })
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpPost("read")]
    public async Task<IActionResult> MarkRead()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        await _dbContext.Notifications
            .Where(notification => notification.RecipientId == userId && notification.ReadAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(
                notification => notification.ReadAtUtc,
                _ => DateTime.UtcNow));

        return NoContent();
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
