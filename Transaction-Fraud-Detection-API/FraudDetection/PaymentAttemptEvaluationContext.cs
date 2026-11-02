using Transaction_Fraud_Detection_API.Models;

namespace Transaction_Fraud_Detection_API.FraudDetection;

public class PaymentAttemptEvaluationContext
{
    public required PaymentAttempt CurrentPaymentAttempt { get; init; }

    public IReadOnlyCollection<PaymentAttempt> PreviousPaymentAttempts { get; init; }
        = Array.Empty<PaymentAttempt>();
}