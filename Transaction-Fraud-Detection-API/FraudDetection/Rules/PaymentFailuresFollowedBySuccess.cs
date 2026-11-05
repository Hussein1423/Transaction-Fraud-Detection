using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection.Interfaces;
namespace Transaction_Fraud_Detection_API.FraudDetection.Rules
{
    public class PaymentFailuresFollowedBySuccess : IPaymentAttemptFraudRule
    {
        public FraudRuleCode RuleCode => FraudRuleCode.PaymentFailuresFollowedBySuccess;
        public FraudRuleResult Evaluate(PaymentAttemptEvaluationContext context)
        {
            if (context.CurrentPaymentAttempt.Status != PaymentAttemptStatus.Succeeded)
            {
                return new FraudRuleResult(false, 0);
            }

            int failedAttemptsCount = context.PreviousFailedAttempts.Count();

            return failedAttemptsCount >= 3
                ? new FraudRuleResult(true, 20)
                : new FraudRuleResult(false, 0);
        }
    }
}
