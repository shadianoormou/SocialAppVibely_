using System.Security.Claims;
using InstagramClone.Api.Data;
using InstagramClone.Api.DTOs;
using InstagramClone.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstagramClone.Api.Controllers;

[ApiController]
[Route("api/posts")]
[Authorize]
public class PostsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public PostsController(AppDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    [HttpPost]
    public async Task<ActionResult<FeedPostResponse>> Create(CreatePostRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var mediaUrl = request.MediaUrl.Trim();
        if (!Uri.TryCreate(mediaUrl, UriKind.Absolute, out var parsedMediaUrl) ||
            (parsedMediaUrl.Scheme != Uri.UriSchemeHttp &&
             parsedMediaUrl.Scheme != Uri.UriSchemeHttps))
            return BadRequest(new { message = "A valid http:// or https:// media URL is required." });

        var mediaType = string.IsNullOrWhiteSpace(request.MediaType)
            ? "image"
            : request.MediaType.Trim().ToLowerInvariant();
        if (mediaType is not ("image" or "video"))
            return BadRequest(new { message = "Media type must be image or video." });

        var post = new Post
        {
            AuthorId = userId,
            Caption = request.Caption?.Trim() ?? string.Empty,
            MediaUrl = mediaUrl,
            MediaType = mediaType,
            Location = string.IsNullOrWhiteSpace(request.Location)
                ? null
                : request.Location.Trim()
        };

        _dbContext.Posts.Add(post);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = post.Id },
            await BuildResponse(post.Id, userId));
    }

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<FeedPostResponse>> GetById(int id)
    {
        var viewerId = GetOptionalUserId();
        var post = await _dbContext.Posts
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.AuthorId, item.Author.IsPrivate })
            .FirstOrDefaultAsync();
        if (post is null)
            return NotFound(new { message = "Post was not found." });

        var canView = !post.IsPrivate ||
            (viewerId.HasValue && (viewerId.Value == post.AuthorId ||
                await _dbContext.Follows.AnyAsync(follow => follow.FollowerId == viewerId.Value && follow.FollowingId == post.AuthorId)));
        if (!canView)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "This post belongs to a private account." });

        return Ok(await BuildResponse(id, viewerId));
    }

    [HttpPost("{id:int}/like")]
    public async Task<ActionResult<object>> ToggleLike(int id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var postAuthorId = await _dbContext.Posts
            .Where(x => x.Id == id)
            .Select(x => (int?)x.AuthorId)
            .SingleOrDefaultAsync();
        if (postAuthorId is null)
            return NotFound(new { message = "Post was not found." });

        var like = await _dbContext.PostLikes.FindAsync(id, userId);
        var liked = like is null;

        if (like is null)
            _dbContext.PostLikes.Add(new PostLike { PostId = id, UserId = userId });
        else
            _dbContext.PostLikes.Remove(like);

        if (liked && postAuthorId.Value != userId)
        {
            _dbContext.Notifications.Add(new Notification
            {
                RecipientId = postAuthorId.Value,
                ActorId = userId,
                Type = "like",
                PostId = id,
                Message = "liked your post"
            });
        }

        await _dbContext.SaveChangesAsync();
        var count = await _dbContext.PostLikes.CountAsync(x => x.PostId == id);
        return Ok(new { liked, likesCount = count });
    }

    [HttpPost("{id:int}/save")]
    public async Task<ActionResult<object>> ToggleSave(int id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var postAuthorId = await _dbContext.Posts
            .Where(x => x.Id == id)
            .Select(x => (int?)x.AuthorId)
            .SingleOrDefaultAsync();
        if (postAuthorId is null)
            return NotFound(new { message = "Post was not found." });

        var save = await _dbContext.PostSaves.FindAsync(id, userId);
        var saved = save is null;

        if (save is null)
            _dbContext.PostSaves.Add(new PostSave { PostId = id, UserId = userId });
        else
            _dbContext.PostSaves.Remove(save);

        await _dbContext.SaveChangesAsync();
        return Ok(new { saved });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<FeedPostResponse>> Update(int id, UpdatePostRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var post = await _dbContext.Posts.FirstOrDefaultAsync(item => item.Id == id);
        if (post is null)
            return NotFound(new { message = "Post was not found." });
        if (post.AuthorId != userId)
            return Forbid();

        post.Caption = request.Caption?.Trim() ?? string.Empty;
        post.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        await _dbContext.SaveChangesAsync();
        return Ok(await BuildResponse(id, userId));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var post = await _dbContext.Posts.FirstOrDefaultAsync(item => item.Id == id);
        if (post is null)
            return NotFound(new { message = "Post was not found." });
        if (post.AuthorId != userId)
            return Forbid();

        var notifications = await _dbContext.Notifications.Where(item => item.PostId == id).ToListAsync();
        _dbContext.Notifications.RemoveRange(notifications);
        _dbContext.Posts.Remove(post);
        await _dbContext.SaveChangesAsync();
        TryDeleteLocalMedia(post.MediaUrl);
        return NoContent();
    }

    [HttpGet("{id:int}/comments")]
    public async Task<ActionResult<IReadOnlyList<CommentResponse>>> GetComments(int id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (!await _dbContext.Posts.AnyAsync(x => x.Id == id))
            return NotFound(new { message = "Post was not found." });

        var comments = await _dbContext.PostComments
            .AsNoTracking()
            .Where(x => x.PostId == id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new CommentResponse
            {
                Id = x.Id,
                UserName = x.Author.UserName,
                ProfileImageUrl = x.Author.ProfileImageUrl,
                Text = x.Text,
                CreatedAtUtc = x.CreatedAtUtc,
                CanDelete = x.AuthorId == userId || x.Post.AuthorId == userId
            })
            .ToListAsync();

        return Ok(comments);
    }

    [HttpPost("{id:int}/comments")]
    public async Task<ActionResult<CommentResponse>> AddComment(
        int id,
        CreateCommentRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var postAuthorId = await _dbContext.Posts
            .Where(x => x.Id == id)
            .Select(x => (int?)x.AuthorId)
            .SingleOrDefaultAsync();
        if (postAuthorId is null)
            return NotFound(new { message = "Post was not found." });

        var text = request.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return BadRequest(new { message = "Comment cannot be empty." });

        var comment = new PostComment
        {
            PostId = id,
            AuthorId = userId,
            Text = text
        };

        _dbContext.PostComments.Add(comment);
        if (postAuthorId.Value != userId)
        {
            _dbContext.Notifications.Add(new Notification
            {
                RecipientId = postAuthorId.Value,
                ActorId = userId,
                Type = "comment",
                PostId = id,
                Message = "commented on your post"
            });
        }
        await _dbContext.SaveChangesAsync();

        var response = await _dbContext.PostComments
            .Where(x => x.Id == comment.Id)
            .Select(x => new CommentResponse
            {
                Id = x.Id,
                UserName = x.Author.UserName,
                ProfileImageUrl = x.Author.ProfileImageUrl,
                Text = x.Text,
                CreatedAtUtc = x.CreatedAtUtc,
                CanDelete = true
            })
            .SingleAsync();

        return Ok(response);
    }

    [HttpDelete("{postId:int}/comments/{commentId:int}")]
    public async Task<IActionResult> DeleteComment(int postId, int commentId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var comment = await _dbContext.PostComments
            .Include(item => item.Post)
            .FirstOrDefaultAsync(item => item.Id == commentId && item.PostId == postId);
        if (comment is null)
            return NotFound(new { message = "Comment was not found." });
        if (comment.AuthorId != userId && comment.Post.AuthorId != userId)
            return Forbid();

        _dbContext.PostComments.Remove(comment);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private async Task<FeedPostResponse> BuildResponse(int id, int? userId) =>
        await _dbContext.Posts
            .AsNoTracking()
            .Where(x => x.Id == id)
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
                IsMine = userId.HasValue && x.AuthorId == userId.Value,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .SingleAsync();

    private bool TryGetUserId(out int userId) =>
        int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);

    private int? GetOptionalUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    private void TryDeleteLocalMedia(string mediaUrl)
    {
        if (!Uri.TryCreate(mediaUrl, UriKind.Absolute, out var uri) ||
            !uri.AbsolutePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
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
