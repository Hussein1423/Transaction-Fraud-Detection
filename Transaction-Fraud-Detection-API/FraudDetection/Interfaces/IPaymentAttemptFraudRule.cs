using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.FraudDetection.Interfaces;

public interface IPaymentAttemptFraudRule
{
    FraudRuleCode RuleCode { get; }

    FraudRuleResult Evaluate(PaymentAttemptEvaluationContext context);
}