namespace InstagramClone.Api.DTOs;

public class NotificationResponse
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string ActorUserName { get; set; } = string.Empty;
    public string? ActorProfileImageUrl { get; set; }
    public int? PostId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool IsRead { get; set; }
}
