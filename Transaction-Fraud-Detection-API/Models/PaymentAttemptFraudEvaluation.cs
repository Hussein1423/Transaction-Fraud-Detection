using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.Models;

[Index(nameof(PaymentAttemptId), IsUnique = true)]
public class PaymentAttemptFraudEvaluation
{
    [Key]
    public Guid PaymentAttemptFraudEvaluationId { get; set; }

    [Required]
    public Guid PaymentAttemptId { get; set; }

    [ForeignKey(nameof(PaymentAttemptId))]
    public PaymentAttempt PaymentAttempt { get; set; } = null!;

    [Required]
    [Range(0, 100)]
    public int RiskScore { get; set; }

    [Required]
    public FraudDecision Decision { get; set; }

    [Required]
    public DateTimeOffset EvaluatedAt { get; set; }
}