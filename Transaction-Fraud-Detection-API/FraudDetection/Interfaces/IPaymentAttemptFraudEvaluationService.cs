using Transaction_Fraud_Detection_API.DTOs;
using Transaction_Fraud_Detection_API.Models;

namespace Transaction_Fraud_Detection_API.FraudDetection;

public interface IPaymentAttemptFraudEvaluationService
{
    FraudEvaluationResultDto Evaluate(PaymentAttemptEvaluationContext context);
}