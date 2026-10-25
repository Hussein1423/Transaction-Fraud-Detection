using Transaction_Fraud_Detection_API.Enums;

namespace Transaction_Fraud_Detection_API.FraudDetection
{
    public class HighAmountRule : IFraudRule
    {
        public FraudRuleCode RuleCode => FraudRuleCode.HighAmount;

        public FraudRuleResult Evaluate(FraudEvaluationContext context)
        {
            if (context.CurrentTransaction.Amount > 1000)
            {
                return new FraudRuleResult(true, 30);
            }

            return new FraudRuleResult(false, 0);
        }   
    }
}
