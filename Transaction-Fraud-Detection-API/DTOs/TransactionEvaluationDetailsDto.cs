
using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.DTOs;

public class TransactionEvaluationDetailsDto
{
    public Guid TransactionId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string MerchantId { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }

    public int RiskScore { get; set; }

    public FraudDecision Decision { get; set; }

    public DateTimeOffset EvaluatedAt { get; set; }
}
