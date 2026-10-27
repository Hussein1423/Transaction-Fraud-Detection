using Microsoft.AspNetCore.Mvc;
using Transaction_Fraud_Detection_API.DTOs;
using Transaction_Fraud_Detection_API.FraudDetection;
namespace Transaction_Fraud_Detection_API.Controllers;

[ApiController]
[Route("api/Transactions")]
public class TransactionsController : ControllerBase
{
    private readonly TransactionEvaluationWorkflowService _transactionEvaluationWorkflowService;

    public TransactionsController(TransactionEvaluationWorkflowService transactionEvaluationWorkflowService)
    {
        _transactionEvaluationWorkflowService = transactionEvaluationWorkflowService;
    }

    [HttpPost("v1")]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequestDto request)
    {
        var result = await _transactionEvaluationWorkflowService.ProcessTransactionAsync(request);
        return Ok(result);
    }


    [HttpGet("{transactionId:guid}/v1")]
    public async Task<IActionResult> GetTransaction(Guid transactionId)
    {
        var transaction = await _transactionEvaluationWorkflowService
            .GetTransactionWithEvaluationAsync(transactionId);

        if (transaction is null)
        {
            return NotFound(new
            {
                message = "Transaction or its evaluation was not found."
            }
            );
        }
        return Ok(transaction);
    }

}