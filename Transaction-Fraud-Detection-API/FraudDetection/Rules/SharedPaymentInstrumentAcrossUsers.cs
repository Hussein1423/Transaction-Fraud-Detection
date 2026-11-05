using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection.Interfaces;

namespace Transaction_Fraud_Detection_API.FraudDetection.Rules;

public class SharedPaymentInstrumentAcrossUsers : IPaymentAttemptFraudRule
{
    public FraudRuleCode RuleCode =>
        FraudRuleCode.SharedPaymentInstrumentAcrossUsers;

    public FraudRuleResult Evaluate(PaymentAttemptEvaluationContext context)
    {
        int distinctUsersCount =
            context.PreviousUserIdsWithSameInstrument.Count + 1;

        return distinctUsersCount >= 4
            ? new FraudRuleResult(true, 30)
            : new FraudRuleResult(false, 0);
    }
}