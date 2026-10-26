using Transaction_Fraud_Detection_API.DTOs;

namespace Transaction_Fraud_Detection_API.FraudDetection
{
    public interface IFraudEvaluationService
    {
        public FraudEvaluationResultDto Evaluate(FraudEvaluationContext context);
    }
}
