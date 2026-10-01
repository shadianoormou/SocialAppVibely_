namespace InstagramClone.Api.Models;

public class PhoneOtpChallenge
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public int Attempts { get; set; }
}
