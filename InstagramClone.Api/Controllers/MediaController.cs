using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    public MediaController(IWebHostEnvironment environment)
    {
        _environment = environment;
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

        var webRoot = _environment.WebRootPath ??
                      Path.Combine(_environment.ContentRootPath, "wwwroot");
        var uploadsPath = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(uploadsPath);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(uploadsPath, fileName);

        await using var stream = System.IO.File.Create(fullPath);
        await file.CopyToAsync(stream);

        var mediaUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";
        var mediaType = isVideo
            ? "video"
            : "image";

        return Ok(new { mediaUrl, mediaType });
    }
}
