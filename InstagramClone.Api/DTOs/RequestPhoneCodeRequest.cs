using System.ComponentModel.DataAnnotations;

namespace InstagramClone.Api.DTOs;

public class RequestPhoneCodeRequest
{
    [Required, Phone, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;
}
