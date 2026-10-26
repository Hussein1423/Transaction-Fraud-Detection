using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.FraudDetection
{
    public class DuplicateLikeTransactionRule : IFraudRule
    {
        public FraudRuleCode RuleCode => FraudRuleCode.DuplicateLikeTransaction;
        public FraudRuleResult Evaluate(FraudEvaluationContext context)
        {
            DateTimeOffset windowStart = context.CurrentTransaction.Timestamp.AddMinutes(-2);
            int transactionsInLastTwoMinutes = context.PreviousTransactions
                .Where(t => t.UserId == context.CurrentTransaction.UserId)
                .Where(t => t.MerchantId == context.CurrentTransaction.MerchantId)
                .Where(t => t.Currency == context.CurrentTransaction.Currency)
                .Where(t => t.Amount == context.CurrentTransaction.Amount)
                .Where(t => t.Timestamp >= windowStart && t.Timestamp <= context.CurrentTransaction.Timestamp)
                .Count();
            transactionsInLastTwoMinutes++; // Include the current transaction in the count
            if (transactionsInLastTwoMinutes >= 2)
            {
                return new FraudRuleResult(true, 50);
            }
            return new FraudRuleResult(false, 0);
        }
    }
}
