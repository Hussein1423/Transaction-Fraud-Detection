
using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection.Interfaces;

namespace Transaction_Fraud_Detection_API.FraudDetection;

public class UnusualTransactionLocationRule : IFraudRule
{
    public FraudRuleCode RuleCode => FraudRuleCode.UnusualTransactionLocation;

    public FraudRuleResult Evaluate(FraudEvaluationContext context)
    {
        var currentTransaction = context.CurrentTransaction;

        // Cannot compare locations if the current location is missing.
        if (string.IsNullOrWhiteSpace(currentTransaction.City) &&
            string.IsNullOrWhiteSpace(currentTransaction.Country))
        {
            return new FraudRuleResult(false, 0);
        }

        var twoHoursAgo = currentTransaction.Timestamp.AddHours(-2);

        var hasDifferentLocation = context.PreviousTransactions
            .Where(t => t.Timestamp >= twoHoursAgo &&  t.Timestamp < currentTransaction.Timestamp)
            // Ignore previous transactions with no location at all.
            .Where(t => !string.IsNullOrWhiteSpace(t.City) ||
                        !string.IsNullOrWhiteSpace(t.Country))
            .Any(t =>
                !string.Equals(
                    t.City?.Trim(),
                    currentTransaction.City?.Trim(),
                    StringComparison.OrdinalIgnoreCase)
                ||
                !string.Equals(
                    t.Country?.Trim(),
                    currentTransaction.Country?.Trim(),
                    StringComparison.OrdinalIgnoreCase));

        return hasDifferentLocation
            ? new FraudRuleResult(true, 15)
            : new FraudRuleResult(false, 0);
    }
}
