using System.ComponentModel.DataAnnotations;

namespace Transaction_Fraud_Detection_API.DTOs;

public class PaymentAttemptRequestDto
{
    [Required]
    [MaxLength(128)]
    public string PaymentInstrumentFingerprint { get; set; } = string.Empty;
}