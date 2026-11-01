using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.FraudDetection
{
    public class HighCumulativeSpending : IFraudRule
    {
        public FraudRuleCode RuleCode => FraudRuleCode.HighCumulativeSpending;

        public FraudRuleResult Evaluate(FraudEvaluationContext context)
        {

            if (context.CurrentTransaction.Currency != "SAR")
            {
                return new FraudRuleResult(false, 0);
            }

            DateTimeOffset currentTimestamp = context.CurrentTransaction.Timestamp;

            var transactions = context.PreviousTransactions
                .Where(t => t.UserId == context.CurrentTransaction.UserId)
                .Where(t => t.Currency == "SAR")
                .Where(t => t.Timestamp <= currentTimestamp)
                .Append(context.CurrentTransaction)
                .ToList();

            // Tier 1: Last 10 minutes > 1,000 SAR
            DateTimeOffset tenMinutesAgo = currentTimestamp.AddMinutes(-10);

            var transactionsInTenMinutes = transactions
                .Where(t => t.Timestamp >= tenMinutesAgo)
                .ToList();

            if (transactionsInTenMinutes.Count >= 2 &&
                transactionsInTenMinutes.Sum(t => t.Amount) > 1000)
            {
                return new FraudRuleResult(true, 20);
            }

            // Tier 2: Last 30 minutes > 1,500 SAR
            DateTimeOffset thirtyMinutesAgo = currentTimestamp.AddMinutes(-30);

            var transactionsInThirtyMinutes = transactions
                .Where(t => t.Timestamp >= thirtyMinutesAgo)
                .ToList();

            if (transactionsInThirtyMinutes.Count >= 2 &&
                transactionsInThirtyMinutes.Sum(t => t.Amount) > 1500)
            {
                return new FraudRuleResult(true, 20);
            }

            // Tier 3: Last 1 hour > 2,000 SAR
            DateTimeOffset oneHourAgo = currentTimestamp.AddHours(-1);

            var transactionsInOneHour = transactions
                .Where(t => t.Timestamp >= oneHourAgo)
                .ToList();

            if (transactionsInOneHour.Count >= 2 &&
                transactionsInOneHour.Sum(t => t.Amount) > 2000)
            {
                return new FraudRuleResult(true, 20);
            }

            // Tier 4: Last 24 hours > 4,500 SAR
            DateTimeOffset twentyFourHoursAgo = currentTimestamp.AddHours(-24);

            var transactionsInTwentyFourHours = transactions
                .Where(t => t.Timestamp >= twentyFourHoursAgo)
                .ToList();

            if (transactionsInTwentyFourHours.Count >= 2 &&
                transactionsInTwentyFourHours.Sum(t => t.Amount) > 4500)
            {
                return new FraudRuleResult(true, 20);
            }

            return new FraudRuleResult(false, 0);
        }
    }
}