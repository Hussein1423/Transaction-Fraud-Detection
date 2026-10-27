using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.DTOs;

public class TriggeredRuleResultDto
{
    public FraudRuleCode RuleCode { get; set; }

    public int PointsAwarded { get; set; }
}
