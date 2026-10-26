using Transaction_Fraud_Detection_API.Enums;
namespace Transaction_Fraud_Detection_API.FraudDetection
{
    public class HighVelocityRule : IFraudRule
    {
        public FraudRuleCode RuleCode => FraudRuleCode.HighVelocity;
        public FraudRuleResult Evaluate(FraudEvaluationContext context)
        {
            DateTimeOffset windowStart = context.CurrentTransaction.Timestamp.AddMinutes(-10);
            int transactionsInLastTenMinutes = context.PreviousTransactions
                .Where(t => t.UserId == context.CurrentTransaction.UserId)
                .Where(t =>  t.Timestamp >= windowStart && t.Timestamp <= context.CurrentTransaction.Timestamp)
                .Count();
            transactionsInLastTenMinutes++; // Include the current transaction in the count
            if (transactionsInLastTenMinutes > 5)
            {
                return new FraudRuleResult(true, 40);
            }
            return new FraudRuleResult(false, 0);
        }
    }
}
