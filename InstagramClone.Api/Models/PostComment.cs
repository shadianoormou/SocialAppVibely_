using System.ComponentModel.DataAnnotations;

namespace InstagramClone.Api.Models;

public class PostComment
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;

    [Required, MaxLength(1000)]
    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
