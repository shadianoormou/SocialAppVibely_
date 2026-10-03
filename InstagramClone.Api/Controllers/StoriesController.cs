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
    private readonly IWebHostEnvironment _environment;

    public StoriesController(AppDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StoryResponse>>> GetActive()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var now = DateTime.UtcNow;
        var stories = await _dbContext.Stories
            .AsNoTracking()
            .Where(x => x.ExpiresAtUtc > now &&
                (!x.Author.IsPrivate ||
                 x.AuthorId == userId ||
                 _dbContext.Follows.Any(follow => follow.FollowerId == userId && follow.FollowingId == x.AuthorId)))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new StoryResponse
            {
                Id = x.Id,
                UserName = x.Author.UserName,
                ProfileImageUrl = x.Author.ProfileImageUrl,
                MediaUrl = x.MediaUrl,
                MediaType = x.MediaType,
                Text = x.Text,
                IsMine = x.AuthorId == userId,
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
            MediaType = string.IsNullOrWhiteSpace(request.MediaType) ? "image" : request.MediaType.Trim().ToLowerInvariant(),
            Text = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim()
        };

        if (story.MediaType is not ("image" or "video"))
            return BadRequest(new { message = "Media type must be image or video." });

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
                MediaType = x.MediaType,
                Text = x.Text,
                IsMine = true,
                CreatedAtUtc = x.CreatedAtUtc,
                ExpiresAtUtc = x.ExpiresAtUtc
            })
            .SingleAsync();

        return Ok(response);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var story = await _dbContext.Stories.FirstOrDefaultAsync(item => item.Id == id);
        if (story is null)
            return NotFound(new { message = "Story was not found." });
        if (story.AuthorId != userId)
            return Forbid();

        _dbContext.Stories.Remove(story);
        await _dbContext.SaveChangesAsync();
        await DeleteMediaAsync(story.MediaUrl);
        return NoContent();
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private async Task DeleteMediaAsync(string mediaUrl)
    {
        if (!Uri.TryCreate(mediaUrl, UriKind.Absolute, out var uri))
            return;

        if (uri.AbsolutePath.StartsWith("/api/media/", StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(Path.GetFileName(uri.LocalPath), out var assetId))
        {
            var asset = await _dbContext.MediaAssets.FindAsync(assetId);
            if (asset is not null)
            {
                _dbContext.MediaAssets.Remove(asset);
                await _dbContext.SaveChangesAsync();
            }
            return;
        }

        if (!uri.AbsolutePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return;
        var fileName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
            return;
        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var path = Path.Combine(webRoot, "uploads", fileName);
        if (System.IO.File.Exists(path))
            System.IO.File.Delete(path);
    }
}
