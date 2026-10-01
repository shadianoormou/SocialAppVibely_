using System.Security.Claims;
using InstagramClone.Api.Data;
using InstagramClone.Api.DTOs;
using InstagramClone.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstagramClone.Api.Controllers;

[ApiController]
[Route("api/stories")]
[Authorize]
public class StoriesController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public StoriesController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StoryResponse>>> GetActive()
    {
        var now = DateTime.UtcNow;
        var stories = await _dbContext.Stories
            .AsNoTracking()
            .Where(x => x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new StoryResponse
            {
                Id = x.Id,
                UserName = x.Author.UserName,
                ProfileImageUrl = x.Author.ProfileImageUrl,
                MediaUrl = x.MediaUrl,
                Text = x.Text,
                CreatedAtUtc = x.CreatedAtUtc,
                ExpiresAtUtc = x.ExpiresAtUtc
            })
            .ToListAsync();

        return Ok(stories);
    }

    [HttpPost]
    public async Task<ActionResult<StoryResponse>> Create(CreateStoryRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var mediaUrl = request.MediaUrl.Trim();
        if (!Uri.TryCreate(mediaUrl, UriKind.Absolute, out var parsedMediaUrl) ||
            (parsedMediaUrl.Scheme != Uri.UriSchemeHttp &&
             parsedMediaUrl.Scheme != Uri.UriSchemeHttps))
            return BadRequest(new { message = "A valid http:// or https:// media URL is required." });

        var story = new Story
        {
            AuthorId = userId,
            MediaUrl = mediaUrl,
            Text = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim()
        };

        _dbContext.Stories.Add(story);
        await _dbContext.SaveChangesAsync();

        var response = await _dbContext.Stories
            .Where(x => x.Id == story.Id)
            .Select(x => new StoryResponse
            {
                Id = x.Id,
                UserName = x.Author.UserName,
                ProfileImageUrl = x.Author.ProfileImageUrl,
                MediaUrl = x.MediaUrl,
                Text = x.Text,
                CreatedAtUtc = x.CreatedAtUtc,
                ExpiresAtUtc = x.ExpiresAtUtc
            })
            .SingleAsync();

        return Ok(response);
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
