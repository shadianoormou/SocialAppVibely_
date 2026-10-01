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

    public PostsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
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
            Caption = request.Caption.Trim(),
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

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FeedPostResponse>> GetById(int id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (!await _dbContext.Posts.AnyAsync(x => x.Id == id))
            return NotFound(new { message = "Post was not found." });

        return Ok(await BuildResponse(id, userId));
    }

    [HttpPost("{id:int}/like")]
    public async Task<ActionResult<object>> ToggleLike(int id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (!await _dbContext.Posts.AnyAsync(x => x.Id == id))
            return NotFound(new { message = "Post was not found." });

        var like = await _dbContext.PostLikes.FindAsync(id, userId);
        var liked = like is null;

        if (like is null)
            _dbContext.PostLikes.Add(new PostLike { PostId = id, UserId = userId });
        else
            _dbContext.PostLikes.Remove(like);

        await _dbContext.SaveChangesAsync();
        var count = await _dbContext.PostLikes.CountAsync(x => x.PostId == id);
        return Ok(new { liked, likesCount = count });
    }

    [HttpPost("{id:int}/save")]
    public async Task<ActionResult<object>> ToggleSave(int id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (!await _dbContext.Posts.AnyAsync(x => x.Id == id))
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

    [HttpGet("{id:int}/comments")]
    public async Task<ActionResult<IReadOnlyList<CommentResponse>>> GetComments(int id)
    {
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
                CreatedAtUtc = x.CreatedAtUtc
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

        if (!await _dbContext.Posts.AnyAsync(x => x.Id == id))
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
        await _dbContext.SaveChangesAsync();

        var response = await _dbContext.PostComments
            .Where(x => x.Id == comment.Id)
            .Select(x => new CommentResponse
            {
                Id = x.Id,
                UserName = x.Author.UserName,
                ProfileImageUrl = x.Author.ProfileImageUrl,
                Text = x.Text,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .SingleAsync();

        return Ok(response);
    }

    private async Task<FeedPostResponse> BuildResponse(int id, int userId) =>
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
                LikedByMe = x.Likes.Any(like => like.UserId == userId),
                SavedByMe = x.Saves.Any(save => save.UserId == userId),
                CreatedAtUtc = x.CreatedAtUtc
            })
            .SingleAsync();

    private bool TryGetUserId(out int userId) =>
        int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
}
