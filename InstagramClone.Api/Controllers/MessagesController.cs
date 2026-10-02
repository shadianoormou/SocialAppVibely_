using System.Security.Claims;
using InstagramClone.Api.Data;
using InstagramClone.Api.DTOs;
using InstagramClone.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstagramClone.Api.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public MessagesController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("inbox")]
    public async Task<ActionResult<IReadOnlyList<MessageThreadResponse>>> GetInbox([FromQuery] int take = 30)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        take = Math.Clamp(take, 1, 100);
        var messages = await _dbContext.DirectMessages
            .AsNoTracking()
            .Include(message => message.Sender)
            .Include(message => message.Recipient)
            .Where(message => message.SenderId == userId || message.RecipientId == userId)
            .OrderByDescending(message => message.CreatedAtUtc)
            .Take(500)
            .ToListAsync();

        var threads = messages
            .GroupBy(message => message.SenderId == userId ? message.RecipientId : message.SenderId)
            .Take(take)
            .Select(group =>
            {
                var latest = group.First();
                var other = latest.SenderId == userId ? latest.Recipient : latest.Sender;
                return new MessageThreadResponse
                {
                    UserName = other.UserName,
                    FullName = other.FullName,
                    ProfileImageUrl = other.ProfileImageUrl,
                    LastMessage = latest.Text,
                    LastMessageAtUtc = latest.CreatedAtUtc,
                    UnreadCount = group.Count(message => message.RecipientId == userId && message.ReadAtUtc == null)
                };
            })
            .ToList();

        return Ok(threads);
    }

    [HttpGet("{userName}")]
    public async Task<ActionResult<IReadOnlyList<MessageResponse>>> GetConversation(string userName, [FromQuery] int take = 100)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var other = await _dbContext.Users.FirstOrDefaultAsync(user => user.UserName == userName.Trim().ToLowerInvariant());
        if (other is null)
            return NotFound(new { message = "User was not found." });
        if (other.Id == userId)
            return BadRequest(new { message = "You cannot message yourself." });

        take = Math.Clamp(take, 1, 200);
        await _dbContext.DirectMessages
            .Where(message => message.SenderId == other.Id && message.RecipientId == userId && message.ReadAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(message => message.ReadAtUtc, _ => DateTime.UtcNow));

        var messages = await _dbContext.DirectMessages
            .AsNoTracking()
            .Where(message =>
                (message.SenderId == userId && message.RecipientId == other.Id) ||
                (message.SenderId == other.Id && message.RecipientId == userId))
            .OrderByDescending(message => message.CreatedAtUtc)
            .Take(take)
            .Select(message => new MessageResponse
            {
                Id = message.Id,
                SenderUserName = message.Sender.UserName,
                SenderProfileImageUrl = message.Sender.ProfileImageUrl,
                RecipientUserName = message.Recipient.UserName,
                RecipientProfileImageUrl = message.Recipient.ProfileImageUrl,
                Text = message.Text,
                CreatedAtUtc = message.CreatedAtUtc,
                IsMine = message.SenderId == userId,
                IsRead = message.ReadAtUtc != null || message.RecipientId != userId
            })
            .ToListAsync();

        messages.Reverse();
        return Ok(messages);
    }

    [HttpPost("{userName}")]
    public async Task<ActionResult<MessageResponse>> Send(string userName, SendMessageRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var text = request.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return BadRequest(new { message = "Message cannot be empty." });

        var recipient = await _dbContext.Users.FirstOrDefaultAsync(user => user.UserName == userName.Trim().ToLowerInvariant());
        if (recipient is null)
            return NotFound(new { message = "User was not found." });
        if (recipient.Id == userId)
            return BadRequest(new { message = "You cannot message yourself." });

        var message = new DirectMessage
        {
            SenderId = userId,
            RecipientId = recipient.Id,
            Text = text
        };
        _dbContext.DirectMessages.Add(message);
        _dbContext.Notifications.Add(new Notification
        {
            RecipientId = recipient.Id,
            ActorId = userId,
            Type = "message",
            Message = "sent you a message"
        });
        await _dbContext.SaveChangesAsync();

        return await GetConversationMessage(message.Id, userId);
    }

    private async Task<ActionResult<MessageResponse>> GetConversationMessage(int messageId, int userId)
    {
        var response = await _dbContext.DirectMessages
            .AsNoTracking()
            .Where(message => message.Id == messageId)
            .Select(message => new MessageResponse
            {
                Id = message.Id,
                SenderUserName = message.Sender.UserName,
                SenderProfileImageUrl = message.Sender.ProfileImageUrl,
                RecipientUserName = message.Recipient.UserName,
                RecipientProfileImageUrl = message.Recipient.ProfileImageUrl,
                Text = message.Text,
                CreatedAtUtc = message.CreatedAtUtc,
                IsMine = message.SenderId == userId,
                IsRead = false
            })
            .FirstAsync();
        return Ok(response);
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
