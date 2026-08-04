namespace InstagramClone.Mobile.Models;

public class LoginRequest
{
    public string EmailOrUserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}