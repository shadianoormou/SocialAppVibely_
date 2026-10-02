namespace InstagramClone.Api.DTOs;

public class MessageResponse
{
    public int Id { get; set; }
    public string SenderUserName { get; set; } = string.Empty;
    public string? SenderProfileImageUrl { get; set; }
    public string RecipientUserName { get; set; } = string.Empty;
    public string? RecipientProfileImageUrl { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool IsMine { get; set; }
    public bool IsRead { get; set; }
}

public class MessageThreadResponse
{
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string LastMessage { get; set; } = string.Empty;
    public DateTime LastMessageAtUtc { get; set; }
    public int UnreadCount { get; set; }
}
