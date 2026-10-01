using System.ComponentModel.DataAnnotations;

namespace InstagramClone.Api.DTOs;

public class VerifyPhoneCodeRequest
{
    [Required, Phone, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, RegularExpression("^\\d{6}$")]
    public string Code { get; set; } = string.Empty;
}
