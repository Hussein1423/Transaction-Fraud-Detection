using System.ComponentModel.DataAnnotations;
using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.DTOs;

public class CreateMockAccountChangeEventRequestDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string UserId { get; set; } = string.Empty;

    [EnumDataType(typeof(AccountChangeType))]
    public AccountChangeType Type { get; set; }
}