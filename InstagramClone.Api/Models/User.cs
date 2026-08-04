using System.ComponentModel.DataAnnotations;

namespace InstagramClone.Api.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Bio { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ProfileImageUrl { get; set; }

    public bool IsPrivate { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}