using Transaction_Fraud_Detection_API.DTOs;

namespace Transaction_Fraud_Detection_API;

public interface IPaymentAttemptEvaluationWorkflowService
{
    Task<PaymentAttemptProcessingResultDto> RecordPaymentAttemptAsync(Guid transactionId);
}