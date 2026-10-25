
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.Models;

public class TriggeredRule
{
    public Guid TriggeredRuleId { get; set; }

    public Guid FraudEvaluationId { get; set; }

    [ForeignKey(nameof(FraudEvaluationId))]
    public FraudEvaluation FraudEvaluation { get; set; } = null!;

    [Required]
    [MaxLength(30)]
    public FraudRuleCode RuleCode { get; set; }

    public int PointsAwarded { get; set; }
}
