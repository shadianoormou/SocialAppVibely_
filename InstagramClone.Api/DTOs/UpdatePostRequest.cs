using System.ComponentModel.DataAnnotations;

namespace InstagramClone.Api.DTOs;

public class UpdatePostRequest
{
    [MaxLength(2200)]
    public string Caption { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Location { get; set; }
}
