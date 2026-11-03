using Transaction_Fraud_Detection_API.Models;
using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection.Interfaces;

namespace Transaction_Fraud_Detection_API.FraudDetection.Rules
{
    public class MultipleCurrenciesInShortWindow : IFraudRule
    {
        public FraudRuleCode RuleCode => FraudRuleCode.MultipleCurrenciesInShortWindow;

        public FraudRuleResult Evaluate(FraudEvaluationContext context)
        {
            Transaction currentTransaction = context.CurrentTransaction;

            DateTimeOffset tenMinutesAgo = currentTransaction.Timestamp.AddMinutes(-10);

            List<Transaction> transactions = context.PreviousTransactions
                .Where(t =>
                    t.UserId == currentTransaction.UserId &&
                    t.Timestamp >= tenMinutesAgo &&
                    t.Timestamp <= currentTransaction.Timestamp)
                .Append(currentTransaction)
                .ToList();

            bool hasMinimumTransactions = transactions.Count >= 3;

            bool hasMultipleCurrencies = transactions
                .Select(t => t.Currency)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() >= 2;

            if (hasMinimumTransactions && hasMultipleCurrencies)
            {
                return new FraudRuleResult(true, 15);
            }

            return new FraudRuleResult(false, 0);
        }
    }
}
