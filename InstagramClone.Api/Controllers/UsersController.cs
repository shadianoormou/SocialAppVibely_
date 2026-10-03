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
                FollowingCount = _dbContext.Follows.Count(follow => follow.FollowerId == x.Id),
                FollowStatus = "self",
                CanViewPosts = true
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
        if (!TryGetUserId(out var userId))
            return Unauthorized();

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
                x.IsPrivate,
                FollowersCount = _dbContext.Follows.Count(follow => follow.FollowingId == x.Id),
                FollowStatus = _dbContext.Follows.Any(follow => follow.FollowerId == userId && follow.FollowingId == x.Id)
                    ? "following"
                    : _dbContext.FollowRequests.Any(request => request.FollowerId == userId && request.FollowingId == x.Id)
                        ? "requested"
                        : "none"
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
                IsMine = post.AuthorId == userId,
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
        var viewerId = GetOptionalUserId();
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
                FollowingCount = _dbContext.Follows.Count(follow => follow.FollowerId == x.Id),
                FollowStatus = viewerId.HasValue && viewerId.Value == x.Id
                    ? "self"
                    : viewerId.HasValue && _dbContext.Follows.Any(follow => follow.FollowerId == viewerId.Value && follow.FollowingId == x.Id)
                        ? "following"
                        : viewerId.HasValue && _dbContext.FollowRequests.Any(request => request.FollowerId == viewerId.Value && request.FollowingId == x.Id)
                            ? "requested"
                            : "none",
                CanViewPosts = !x.IsPrivate ||
                    (viewerId.HasValue && (viewerId.Value == x.Id || _dbContext.Follows.Any(follow => follow.FollowerId == viewerId.Value && follow.FollowingId == x.Id)))
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
        var target = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.UserName == normalizedUserName)
            .Select(user => new { user.Id, user.IsPrivate })
            .FirstOrDefaultAsync();
        if (target is null)
            return NotFound(new { message = "User was not found." });

        var viewerId = GetOptionalUserId();
        var canView = !target.IsPrivate ||
            (viewerId.HasValue && (viewerId.Value == target.Id ||
                await _dbContext.Follows.AnyAsync(follow => follow.FollowerId == viewerId.Value && follow.FollowingId == target.Id)));
        if (!canView)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "This account is private." });

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
                IsMine = viewerId.HasValue && post.AuthorId == viewerId.Value,
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
        if (follow is not null)
        {
            _dbContext.Follows.Remove(follow);
            await _dbContext.SaveChangesAsync();
            var remainingFollowers = await _dbContext.Follows.CountAsync(x => x.FollowingId == target.Id);
            return Ok(new { status = "none", following = false, requested = false, followersCount = remainingFollowers });
        }

        var existingRequest = await _dbContext.FollowRequests.FindAsync(userId, target.Id);
        if (existingRequest is not null)
        {
            _dbContext.FollowRequests.Remove(existingRequest);
            await _dbContext.SaveChangesAsync();
            var unchangedFollowers = await _dbContext.Follows.CountAsync(x => x.FollowingId == target.Id);
            return Ok(new { status = "none", following = false, requested = false, followersCount = unchangedFollowers });
        }

        if (target.IsPrivate)
        {
            _dbContext.FollowRequests.Add(new FollowRequest { FollowerId = userId, FollowingId = target.Id });
            _dbContext.Notifications.Add(new Notification
            {
                RecipientId = target.Id,
                ActorId = userId,
                Type = "follow_request",
                Message = "requested to follow you"
            });
            await _dbContext.SaveChangesAsync();
            var currentFollowers = await _dbContext.Follows.CountAsync(x => x.FollowingId == target.Id);
            return Ok(new { status = "requested", following = false, requested = true, followersCount = currentFollowers });
        }

        _dbContext.Follows.Add(new Follow { FollowerId = userId, FollowingId = target.Id });
        _dbContext.Notifications.Add(new Notification
        {
            RecipientId = target.Id,
            ActorId = userId,
            Type = "follow",
            Message = "started following you"
        });

        await _dbContext.SaveChangesAsync();
        var followersCount = await _dbContext.Follows.CountAsync(x => x.FollowingId == target.Id);
        return Ok(new { status = "following", following = true, requested = false, followersCount });
    }

    [HttpPost("{userName}/follow-request/{decision}")]
    public async Task<ActionResult<object>> RespondToFollowRequest(string userName, string decision)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var requester = await _dbContext.Users.FirstOrDefaultAsync(user => user.UserName == userName.Trim().ToLowerInvariant());
        if (requester is null)
            return NotFound(new { message = "User was not found." });

        var request = await _dbContext.FollowRequests.FindAsync(requester.Id, userId);
        if (request is null)
            return NotFound(new { message = "Follow request was not found." });

        var normalizedDecision = decision.Trim().ToLowerInvariant();
        if (normalizedDecision is not ("accept" or "decline"))
            return BadRequest(new { message = "Decision must be accept or decline." });

        _dbContext.FollowRequests.Remove(request);
        if (normalizedDecision == "accept")
        {
            _dbContext.Follows.Add(new Follow { FollowerId = requester.Id, FollowingId = userId });
            _dbContext.Notifications.Add(new Notification
            {
                RecipientId = requester.Id,
                ActorId = userId,
                Type = "follow_accepted",
                Message = "accepted your follow request"
            });
        }

        var requestNotifications = await _dbContext.Notifications
            .Where(notification => notification.RecipientId == userId && notification.ActorId == requester.Id && notification.Type == "follow_request")
            .ToListAsync();
        _dbContext.Notifications.RemoveRange(requestNotifications);
        await _dbContext.SaveChangesAsync();

        return Ok(new { accepted = normalizedDecision == "accept" });
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private int? GetOptionalUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
}
