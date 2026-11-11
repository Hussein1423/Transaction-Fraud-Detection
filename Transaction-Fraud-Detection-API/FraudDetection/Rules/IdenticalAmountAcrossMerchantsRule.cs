using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection.Interfaces;
namespace Transaction_Fraud_Detection_API.FraudDetection
{
    public class IdenticalAmountAcrossMerchantsRule : IFraudRule
    {
        public FraudRuleCode RuleCode => FraudRuleCode.IdenticalAmountAcrossMerchants;
        public FraudRuleResult Evaluate(FraudEvaluationContext context)
        {
            DateTimeOffset windowStart = context.CurrentTransaction.Timestamp.AddMinutes(-5);

            var transactionsInWindow = context.PreviousTransactions
                .Where(t => t.Currency == context.CurrentTransaction.Currency)
                .Where(t => t.Timestamp >= windowStart &&
                            t.Timestamp <= context.CurrentTransaction.Timestamp)
                .Append(context.CurrentTransaction)
                .ToList();

            int distinctMerchantsCount = transactionsInWindow
                .Select(t => t.MerchantId)
                .Distinct()
                .Count();

            if (distinctMerchantsCount < 4)
            {
                return new FraudRuleResult(false, 0);
            }

            decimal minAmount = transactionsInWindow.Min(t => t.Amount);
            decimal maxAmount = transactionsInWindow.Max(t => t.Amount);

            if (maxAmount - minAmount > 3)
            {
                return new FraudRuleResult(false, 0);
            }

            bool allAmountsAreWholeNumbers = transactionsInWindow
                .All(t => t.Amount % 1 == 0);

            int pointsAwarded = allAmountsAreWholeNumbers ? 10 : 20;

            return new FraudRuleResult(true, pointsAwarded);
        }
    }
}
