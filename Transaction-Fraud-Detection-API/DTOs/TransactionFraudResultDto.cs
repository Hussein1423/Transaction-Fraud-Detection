namespace Transaction_Fraud_Detection_API.DTOs
{
    public class TransactionFraudResultDto
    {
        public Guid TransactionId { get; set; }
        public int RiskScore { get; set; }
        public List<TriggeredRuleResultDto> TriggeredRules { get; set; } = new();
    }
}
