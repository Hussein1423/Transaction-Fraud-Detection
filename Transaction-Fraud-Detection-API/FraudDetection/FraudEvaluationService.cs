using Transaction_Fraud_Detection_API.DTOs;
namespace Transaction_Fraud_Detection_API.FraudDetection
{
    public class FraudEvaluationService : IFraudEvaluationService
    {
        private readonly IEnumerable<IFraudRule> _fraudRules;

        public FraudEvaluationService(IEnumerable<IFraudRule> fraudRules)
        {
            _fraudRules = fraudRules;
        }

        public FraudEvaluationResultDto Evaluate(FraudEvaluationContext context)
        {
            int totalRiskScore = 0;
            foreach (var rule in _fraudRules)
            {
                FraudRuleResult fraudRuleResult = rule.Evaluate(context);
                if (fraudRuleResult.IsTriggered)
                    totalRiskScore += fraudRuleResult.PointsAwarded;
            }
            totalRiskScore = Math.Min(totalRiskScore, 100); // Cap the risk score at 100
            var decision = totalRiskScore >= 50 ? Enums.FraudDecision.FlagForReview : Enums.FraudDecision.Allow;
            return new FraudEvaluationResultDto
            {
                RiskScore = totalRiskScore,
                Decision = decision
            };

        }
    }
}
