namespace InstagramClone.Mobile.Models;

public class CreatePostRequest
{
    public string Caption { get; set; } = string.Empty;
    public string MediaUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = "image";
    public string? Location { get; set; }
}
