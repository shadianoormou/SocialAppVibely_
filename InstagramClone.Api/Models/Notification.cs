using System.ComponentModel.DataAnnotations;

namespace InstagramClone.Api.Models;

public class Notification
{
    public int Id { get; set; }
    public int RecipientId { get; set; }
    public User Recipient { get; set; } = null!;
    public int ActorId { get; set; }
    public User Actor { get; set; } = null!;
    public string Type { get; set; } = string.Empty;
    public int? PostId { get; set; }

    [MaxLength(180)]
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
}
