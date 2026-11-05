using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.DTOs
{
    public class CombinedFraudEvaluationResultDto
    {
        public TransactionFraudResultDto Transaction { get; set; } = new();

        public PaymentAttemptFraudResultDto PaymentAttempt { get; set; } = new();

        public int TotalRiskScore { get; set; }

        public List<CombinedTriggeredRuleResultDto> CombinedTriggeredRules { get; set; } = new();

        public FraudDecision FinalDecision { get; set; }
    }
}
