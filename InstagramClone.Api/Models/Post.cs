using System.ComponentModel.DataAnnotations;

namespace InstagramClone.Api.Models;

public class Post
{
    public int Id { get; set; }
    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;

    [Required, MaxLength(2200)]
    public string Caption { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string MediaUrl { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string MediaType { get; set; } = "image";

    [MaxLength(150)]
    public string? Location { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<PostLike> Likes { get; set; } = new List<PostLike>();
    public ICollection<PostSave> Saves { get; set; } = new List<PostSave>();
    public ICollection<PostComment> Comments { get; set; } = new List<PostComment>();
}
