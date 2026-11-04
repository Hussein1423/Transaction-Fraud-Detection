using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection.Interfaces;
namespace Transaction_Fraud_Detection_API.FraudDetection.Rules
{
    public class PaymentFailuresFollowedBySuccess : IPaymentAttemptFraudRule
    {
        public FraudRuleCode RuleCode => FraudRuleCode.PaymentFailuresFollowedBySuccess;
        public FraudRuleResult Evaluate(PaymentAttemptEvaluationContext context)
        { 
            if(context.CurrentPaymentAttempt.Status != PaymentAttemptStatus.Succeeded)
            {
                return new FraudRuleResult(false, 0);
            }
            DateTimeOffset windowStart = context.CurrentPaymentAttempt.AttemptedAt.AddMinutes(-30);

            DateTimeOffset? lastSuccessfulAttemptAt = context.PreviousPaymentAttempts
            .Where(attempt => attempt.Transaction.UserId == context.CurrentPaymentAttempt.Transaction.UserId)
            .Where(attempt => attempt.Status == PaymentAttemptStatus.Succeeded)
            .Where(attempt => attempt.AttemptedAt < context.CurrentPaymentAttempt.AttemptedAt)
            .Select(attempt => (DateTimeOffset?)attempt.AttemptedAt)
            .Max();

            int failedAttemptsCount =
                context.PreviousPaymentAttempts
                .Where(attempt => attempt.Transaction.UserId == context.CurrentPaymentAttempt.Transaction.UserId)
                .Where(attempt => lastSuccessfulAttemptAt == null || attempt.AttemptedAt > lastSuccessfulAttemptAt.Value)
                .Where(attempt => attempt.AttemptedAt >= windowStart && attempt.AttemptedAt < context.CurrentPaymentAttempt.AttemptedAt)
                .Where(attempt => attempt.Status == PaymentAttemptStatus.Failed)
                .Count();
            if(failedAttemptsCount >= 3)
            {
                return new FraudRuleResult(true, 20);
            }
            return new FraudRuleResult(false, 0);

        }
    }
}
