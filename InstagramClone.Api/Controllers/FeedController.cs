using System.Security.Claims;
using InstagramClone.Api.Data;
using InstagramClone.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstagramClone.Api.Controllers;

[ApiController]
[Route("api/feed")]
public class FeedController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public FeedController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<FeedPostResponse>>> GetFeed(
        [FromQuery] int take = 30)
    {
        int? userId = null;
        if (int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var authenticatedUserId))
        {
            userId = authenticatedUserId;
        }

        take = Math.Clamp(take, 1, 50);

        var posts = await _dbContext.Posts
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => new FeedPostResponse
            {
                Id = x.Id,
                UserName = x.Author.UserName,
                FullName = x.Author.FullName,
                ProfileImageUrl = x.Author.ProfileImageUrl,
                Caption = x.Caption,
                MediaUrl = x.MediaUrl,
                MediaType = x.MediaType,
                Location = x.Location,
                LikesCount = x.Likes.Count,
                CommentsCount = x.Comments.Count,
                LikedByMe = userId.HasValue && x.Likes.Any(like => like.UserId == userId.Value),
                SavedByMe = userId.HasValue && x.Saves.Any(save => save.UserId == userId.Value),
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(posts);
    }

}
