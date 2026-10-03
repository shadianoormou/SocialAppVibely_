using System.ComponentModel.DataAnnotations;

namespace InstagramClone.Api.Models;

public class MediaAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int OwnerId { get; set; }

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    public byte[] Data { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
