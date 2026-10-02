using System.Security.Claims;
using InstagramClone.Api.Data;
using InstagramClone.Api.DTOs;
using InstagramClone.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstagramClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public UsersController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserProfileResponse>> GetMyProfile()
    {
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid authentication token."
            });
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new UserProfileResponse
            {
                Id = x.Id,
                UserName = x.UserName,
                Email = x.Email,
                FullName = x.FullName,
                Bio = x.Bio,
                ProfileImageUrl = x.ProfileImageUrl,
                ProfileLinkTitle = x.ProfileLinkTitle,
                ProfileLinkUrl = x.ProfileLinkUrl,
                IsPrivate = x.IsPrivate,
                CreatedAtUtc = x.CreatedAtUtc,
                PostsCount = _dbContext.Posts.Count(post => post.AuthorId == x.Id),
                FollowersCount = _dbContext.Follows.Count(follow => follow.FollowingId == x.Id),
                FollowingCount = _dbContext.Follows.Count(follow => follow.FollowerId == x.Id)
            })
            .FirstOrDefaultAsync();

        if (user is null)
        {
            return NotFound(new
            {
                message = "User was not found."
            });
        }

        return Ok(user);
    }

    [HttpGet("search")]
    public async Task<ActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Array.Empty<object>());

        var term = q.Trim().ToLowerInvariant();
        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.UserName.Contains(term) || x.FullName.Contains(term))
            .OrderBy(x => x.UserName)
            .Take(30)
            .Select(x => new
            {
                x.Id,
                x.UserName,
                x.FullName,
                x.ProfileImageUrl,
                FollowersCount = _dbContext.Follows.Count(follow => follow.FollowingId == x.Id)
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserProfileResponse>> UpdateMyProfile(
        UpdateProfileRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
            return NotFound(new { message = "User was not found." });

        var normalizedUserName = request.UserName.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedUserName))
            return BadRequest(new { message = "Username is required." });

        var usernameTaken = await _dbContext.Users
            .AnyAsync(x => x.UserName == normalizedUserName && x.Id != userId);
        if (usernameTaken)
            return Conflict(new { message = "Username is already taken." });

        var linkTitle = request.ProfileLinkTitle.Trim();
        var linkUrl = request.ProfileLinkUrl.Trim();
        if (string.IsNullOrWhiteSpace(linkTitle) != string.IsNullOrWhiteSpace(linkUrl))
            return BadRequest(new { message = "A link needs both a title and a URL." });
        if (!string.IsNullOrWhiteSpace(linkUrl) &&
            (!Uri.TryCreate(linkUrl, UriKind.Absolute, out var parsedLink) ||
             (parsedLink.Scheme != Uri.UriSchemeHttp && parsedLink.Scheme != Uri.UriSchemeHttps)))
            return BadRequest(new { message = "Link URL must start with http:// or https://." });

        user.UserName = normalizedUserName;
        user.FullName = request.FullName.Trim();
        user.Bio = request.Bio.Trim();
        user.ProfileImageUrl = string.IsNullOrWhiteSpace(request.ProfileImageUrl)
            ? null
            : request.ProfileImageUrl.Trim();
        user.ProfileLinkTitle = linkTitle;
        user.ProfileLinkUrl = linkUrl;
        user.IsPrivate = request.IsPrivate;
        await _dbContext.SaveChangesAsync();

        return await GetMyProfile();
    }

    [HttpGet("me/posts")]
    public async Task<ActionResult<IReadOnlyList<FeedPostResponse>>> GetMyPosts(
        [FromQuery] string tab = "posts")
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var query = _dbContext.Posts.AsNoTracking();
        query = tab.Trim().ToLowerInvariant() switch
        {
            "saved" => query.Where(post => post.Saves.Any(save => save.UserId == userId)),
            _ => query.Where(post => post.AuthorId == userId)
        };

        var posts = await query
            .OrderByDescending(post => post.CreatedAtUtc)
            .Select(post => new FeedPostResponse
            {
                Id = post.Id,
                UserName = post.Author.UserName,
                FullName = post.Author.FullName,
                ProfileImageUrl = post.Author.ProfileImageUrl,
                Caption = post.Caption,
                MediaUrl = post.MediaUrl,
                MediaType = post.MediaType,
                Location = post.Location,
                LikesCount = post.Likes.Count,
                CommentsCount = post.Comments.Count,
                LikedByMe = post.Likes.Any(like => like.UserId == userId),
                SavedByMe = post.Saves.Any(save => save.UserId == userId),
                CreatedAtUtc = post.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(posts);
    }

    [AllowAnonymous]
    [HttpGet("public/{userName}")]
    public async Task<ActionResult<UserProfileResponse>> GetPublicProfile(string userName)
    {
        var normalizedUserName = userName.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.UserName == normalizedUserName)
            .Select(x => new UserProfileResponse
            {
                Id = x.Id,
                UserName = x.UserName,
                Email = string.Empty,
                FullName = x.FullName,
                Bio = x.Bio,
                ProfileImageUrl = x.ProfileImageUrl,
                ProfileLinkTitle = x.ProfileLinkTitle,
                ProfileLinkUrl = x.ProfileLinkUrl,
                IsPrivate = x.IsPrivate,
                CreatedAtUtc = x.CreatedAtUtc,
                PostsCount = _dbContext.Posts.Count(post => post.AuthorId == x.Id),
                FollowersCount = _dbContext.Follows.Count(follow => follow.FollowingId == x.Id),
                FollowingCount = _dbContext.Follows.Count(follow => follow.FollowerId == x.Id)
            })
            .FirstOrDefaultAsync();

        return user is null
            ? NotFound(new { message = "User was not found." })
            : Ok(user);
    }

    [AllowAnonymous]
    [HttpGet("public/{userName}/posts")]
    public async Task<ActionResult<IReadOnlyList<FeedPostResponse>>> GetPublicPosts(string userName)
    {
        var normalizedUserName = userName.Trim().ToLowerInvariant();
        var posts = await _dbContext.Posts
            .AsNoTracking()
            .Where(post => post.Author.UserName == normalizedUserName)
            .OrderByDescending(post => post.CreatedAtUtc)
            .Select(post => new FeedPostResponse
            {
                Id = post.Id,
                UserName = post.Author.UserName,
                FullName = post.Author.FullName,
                ProfileImageUrl = post.Author.ProfileImageUrl,
                Caption = post.Caption,
                MediaUrl = post.MediaUrl,
                MediaType = post.MediaType,
                Location = post.Location,
                LikesCount = post.Likes.Count,
                CommentsCount = post.Comments.Count,
                LikedByMe = false,
                SavedByMe = false,
                CreatedAtUtc = post.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(posts);
    }

    [HttpPost("{userName}/follow")]
    public async Task<ActionResult<object>> ToggleFollow(string userName)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var target = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.UserName == userName.Trim().ToLower());

        if (target is null)
            return NotFound(new { message = "User was not found." });

        if (target.Id == userId)
            return BadRequest(new { message = "You cannot follow yourself." });

        var follow = await _dbContext.Follows.FindAsync(userId, target.Id);
        var following = follow is null;

        if (follow is null)
        {
            _dbContext.Follows.Add(new Follow { FollowerId = userId, FollowingId = target.Id });
            _dbContext.Notifications.Add(new Notification
            {
                RecipientId = target.Id,
                ActorId = userId,
                Type = "follow",
                Message = "started following you"
            });
        }
        else
            _dbContext.Follows.Remove(follow);

        await _dbContext.SaveChangesAsync();
        var followersCount = await _dbContext.Follows.CountAsync(x => x.FollowingId == target.Id);
        return Ok(new { following, followersCount });
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
