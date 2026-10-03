using System.Security.Claims;
using InstagramClone.Api.Data;
using InstagramClone.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstagramClone.Api.Controllers;

[ApiController]
[Route("api/media")]
[Authorize]
public class MediaController : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".mp4", ".mov"
        };

    private readonly IWebHostEnvironment _environment;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public MediaController(
        IWebHostEnvironment environment,
        AppDbContext dbContext,
        IConfiguration configuration)
    {
        _environment = environment;
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<object>> Upload(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "The uploaded file is empty." });

        if (file.Length > 50_000_000)
            return BadRequest(new { message = "Media must be 50 MB or smaller." });

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            return BadRequest(new { message = "This file type is not supported." });

        var isVideo = extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                      extension.Equals(".mov", StringComparison.OrdinalIgnoreCase);
        var expectedPrefix = isVideo ? "video/" : "image/";
        if (string.IsNullOrWhiteSpace(file.ContentType) ||
            !file.ContentType.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "The uploaded media type does not match its file extension." });

        if (UseDatabaseStorage())
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();

            await using var memory = new MemoryStream();
            await file.CopyToAsync(memory);
            var asset = new MediaAsset
            {
                OwnerId = userId,
                FileName = Path.GetFileName(file.FileName),
                ContentType = file.ContentType,
                Data = memory.ToArray()
            };
            _dbContext.MediaAssets.Add(asset);
            await _dbContext.SaveChangesAsync();

            var databaseMediaUrl = $"{Request.Scheme}://{Request.Host}/api/media/{asset.Id:N}";
            return Ok(new { mediaUrl = databaseMediaUrl, mediaType = isVideo ? "video" : "image" });
        }

        var webRoot = _environment.WebRootPath ??
                      Path.Combine(_environment.ContentRootPath, "wwwroot");
        var uploadsPath = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(uploadsPath);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(uploadsPath, fileName);

        await using var stream = System.IO.File.Create(fullPath);
        await file.CopyToAsync(stream);

        var mediaUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";
        return Ok(new { mediaUrl, mediaType = isVideo ? "video" : "image" });
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Get(Guid id)
    {
        var asset = await _dbContext.MediaAssets
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Data, item.ContentType })
            .SingleOrDefaultAsync();

        return asset is null
            ? NotFound()
            : File(asset.Data, asset.ContentType, enableRangeProcessing: true);
    }

    private bool UseDatabaseStorage() =>
        string.Equals(
            _configuration["MediaStorage:Provider"],
            "Database",
            StringComparison.OrdinalIgnoreCase);
}
