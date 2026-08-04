namespace InstagramClone.Api.DTOs;

public class UserProfileResponse
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Bio { get; set; } = string.Empty;

    public string? ProfileImageUrl { get; set; }

    public bool IsPrivate { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}