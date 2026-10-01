namespace InstagramClone.Mobile.Models;

public class PhoneCodeResponse
{
    public string Message { get; set; } = string.Empty;
    public int ExpiresInSeconds { get; set; }
    public string? DevelopmentCode { get; set; }
}
