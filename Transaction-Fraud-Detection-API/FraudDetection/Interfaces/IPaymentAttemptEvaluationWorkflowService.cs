using Transaction_Fraud_Detection_API.DTOs;

namespace Transaction_Fraud_Detection_API.FraudDetection.Interfaces;

public interface IPaymentAttemptEvaluationWorkflowService
{
    Task<CombinedFraudEvaluationResultDto> RecordPaymentAttemptAsync(Guid transactionId, PaymentAttemptRequestDto request);
}