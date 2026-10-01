namespace InstagramClone.Mobile.Models;

public class Story
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public string? Text { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
