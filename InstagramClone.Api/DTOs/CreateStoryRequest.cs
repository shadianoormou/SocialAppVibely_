using System.ComponentModel.DataAnnotations;

namespace InstagramClone.Api.DTOs;

public class CreateStoryRequest
{
    [Required, MaxLength(1000)]
    public string MediaUrl { get; set; } = string.Empty;

    [MaxLength(180)]
    public string? Text { get; set; }
}
