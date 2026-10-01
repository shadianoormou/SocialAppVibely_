namespace InstagramClone.Api.DTOs;

public class FeedPostResponse
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string Caption { get; set; } = string.Empty;
    public string MediaUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = "image";
    public string? Location { get; set; }
    public int LikesCount { get; set; }
    public int CommentsCount { get; set; }
    public bool LikedByMe { get; set; }
    public bool SavedByMe { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
