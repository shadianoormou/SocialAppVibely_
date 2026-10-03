using System.ComponentModel.DataAnnotations;

namespace InstagramClone.Api.Models;

public class Story
{
    public int Id { get; set; }
    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;

    [Required, MaxLength(1000)]
    public string MediaUrl { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string MediaType { get; set; } = "image";

    [MaxLength(180)]
    public string? Text { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddHours(24);
}
