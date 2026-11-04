using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.DTOs
{
    public class PaymentAttemptFraudResultDto
    {
        public Guid PaymentAttemptId { get; set; }
        public PaymentAttemptStatus Status { get; set; }
        public int? RiskScore { get; set; }
        public List<TriggeredRuleResultDto> TriggeredRules { get; set; } = new();
    }
}
