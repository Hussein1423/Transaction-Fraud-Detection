using System.ComponentModel.DataAnnotations;

namespace Transaction_Fraud_Detection_API.DTOs;

public class PaymentAttemptRequestDto
{
    [Required]
    [StringLength(250, MinimumLength = 1)]
    public string PaymentInstrumentFingerprint { get; set; } = string.Empty;
}