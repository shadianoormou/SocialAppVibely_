using System.ComponentModel.DataAnnotations;

namespace InstagramClone.Api.DTOs;

public class SendMessageRequest
{
    [Required, MaxLength(2000)]
    public string Text { get; set; } = string.Empty;
}
