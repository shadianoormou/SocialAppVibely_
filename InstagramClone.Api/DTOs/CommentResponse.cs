namespace InstagramClone.Api.DTOs;

public class CommentResponse
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool CanDelete { get; set; }
}
