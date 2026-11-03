using System.ComponentModel.DataAnnotations;
using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.DTOs;

public class PaymentAttemptProcessingResultDto
{
    [Required]
    public Guid PaymentAttemptId { get; set; }

    [Required]
    public PaymentAttemptStatus Status { get; set; }

    [MaxLength(250)]
    public string? FailureReason { get; set; }

    [Range(0, 100)]
    public int? RiskScore { get; set; }

    public FraudDecision? Decision { get; set; }
}