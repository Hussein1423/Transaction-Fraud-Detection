using System.ComponentModel.DataAnnotations;

namespace Transaction_Fraud_Detection_API.DTOs;

public class CreateTransactionRequestDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Range(typeof(decimal), "1.0", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [Required]
    [RegularExpression(@"^[A-Za-z]{3}$")]
    public string Currency { get; set; } = string.Empty;

    [Required]
    public string MerchantId { get; set; } = string.Empty;
}