using System.ComponentModel.DataAnnotations;

namespace InstagramClone.Api.DTOs;

public class UpdateProfileRequest
{
    [Required, MaxLength(50)]
    public string UserName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Bio { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ProfileImageUrl { get; set; }

    [MaxLength(80)]
    public string ProfileLinkTitle { get; set; } = string.Empty;

    [MaxLength(500)]
    public string ProfileLinkUrl { get; set; } = string.Empty;

    public bool IsPrivate { get; set; }
}
