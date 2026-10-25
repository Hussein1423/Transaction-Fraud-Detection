
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.Models;

[Index(nameof(TransactionId), IsUnique = true)]
public class FraudEvaluation
{
    public Guid FraudEvaluationId { get; set; }

    public Guid TransactionId { get; set; }

    [ForeignKey(nameof(TransactionId))]
    public Transaction Transaction { get; set; } = null!;

    [Range(0, 100)]
    public int RiskScore { get; set; }

    public FraudDecision Decision { get; set; }

    public DateTimeOffset EvaluatedAt { get; set; }
}
