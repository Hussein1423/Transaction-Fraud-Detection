using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.DTOs;

public class FraudEvaluationResultDto
{
    public int RiskScore { get; set; }

    public FraudDecision Decision { get; set; }
}