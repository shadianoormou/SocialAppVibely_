using System.ComponentModel.DataAnnotations;

namespace InstagramClone.Api.DTOs;

public class CreatePostRequest
{
    [Required, MaxLength(2200)]
    public string Caption { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string MediaUrl { get; set; } = string.Empty;

    [MaxLength(20)]
    public string MediaType { get; set; } = "image";

    [MaxLength(150)]
    public string? Location { get; set; }
}
