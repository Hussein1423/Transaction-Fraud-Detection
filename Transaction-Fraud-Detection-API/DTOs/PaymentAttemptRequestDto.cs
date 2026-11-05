using System.ComponentModel.DataAnnotations;

namespace Transaction_Fraud_Detection_API.DTOs;

public class PaymentAttemptRequestDto
{
    [Required]
    [StringLength(64, MinimumLength = 64)]
    public string PaymentInstrumentFingerprint { get; set; } = string.Empty;
}