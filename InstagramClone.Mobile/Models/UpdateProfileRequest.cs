namespace InstagramClone.Mobile.Models;

public class UpdateProfileRequest
{
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string ProfileLinkTitle { get; set; } = string.Empty;
    public string ProfileLinkUrl { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
}
