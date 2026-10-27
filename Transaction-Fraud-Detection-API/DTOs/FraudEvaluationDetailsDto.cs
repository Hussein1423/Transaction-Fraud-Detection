using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.DTOs;

public class FraudEvaluationDetailsDto
{
    public Guid TransactionId { get; set; }

    public int RiskScore { get; set; }

    public FraudDecision Decision { get; set; }

    public DateTimeOffset EvaluatedAt { get; set; }

    public List<TriggeredRuleResultDto> TriggeredRules { get; set; } = new();
}