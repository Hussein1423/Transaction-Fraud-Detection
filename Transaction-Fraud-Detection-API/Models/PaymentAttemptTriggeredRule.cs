using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.Models;

public class PaymentAttemptTriggeredRule
{
    [Key]
    public Guid PaymentAttemptTriggeredRuleId { get; set; }

    [Required]
    public Guid PaymentAttemptFraudEvaluationId { get; set; }

    [ForeignKey(nameof(PaymentAttemptFraudEvaluationId))]
    public PaymentAttemptFraudEvaluation PaymentAttemptFraudEvaluation { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public FraudRuleCode RuleCode { get; set; }

    [Required]
    public int PointsAwarded { get; set; }
}