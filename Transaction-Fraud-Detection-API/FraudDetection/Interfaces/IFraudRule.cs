using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.Models;
namespace Transaction_Fraud_Detection_API.FraudDetection.Interfaces
{
    public interface IFraudRule
    {
        public FraudRuleCode RuleCode { get; }

        public FraudRuleResult Evaluate(FraudEvaluationContext context);
    }
}
