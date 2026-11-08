using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection.Interfaces;

namespace Transaction_Fraud_Detection_API.FraudDetection.Rules;

public class TransactionAfterAccountDetailsChangeRule : IFraudRule
{
    public FraudRuleCode RuleCode =>
        FraudRuleCode.TransactionAfterAccountDetailsChange;

    public FraudRuleResult Evaluate(FraudEvaluationContext context)
    {
        var distinctTypes = context.RecentAccountChangeEvents
            .Select(e => e.Type)
            .Distinct()
            .ToList();

        if (distinctTypes.Count == 0)
        {
            return new FraudRuleResult(false, 0);
        }

        var highestBaseScore = distinctTypes
            .Max(GetBaseScore);

        var score = distinctTypes.Count == 1
            ? highestBaseScore
            : highestBaseScore + (5 * distinctTypes.Count);

        return new FraudRuleResult(true, score);
    }

    private int GetBaseScore(AccountChangeType type)
    {
        return type switch
        {
            AccountChangeType.EmailAddress => 20,
            AccountChangeType.PhoneNumber => 30,
            AccountChangeType.Password => 35,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}