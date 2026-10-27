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
    [ProducesResponseType(typeof(FraudEvaluationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequestDto request)
    {
        var result = await _transactionEvaluationWorkflowService.ProcessTransactionAsync(request);
        return Ok(result);
    }


    [HttpGet("{transactionId:guid}/v1")]
    [ProducesResponseType(typeof(FraudEvaluationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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


    [HttpGet("flagged/v1")]
    [ProducesResponseType(typeof(FraudEvaluationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFlaggedTransactions()
    {
        var transactions = await _transactionEvaluationWorkflowService
            .GetFlaggedTransactionsAsync();
        if(transactions is null || !transactions.Any())
        {
            return NotFound(new
            {
                message = "No flagged transactions found."
            });
        }
        return Ok(transactions);
    }

    [HttpGet("{transactionId:guid}/evaluation/v1")]
    [ProducesResponseType(typeof(FraudEvaluationDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFraudEvaluationDetails(Guid transactionId)
    {
        var evaluation = await _transactionEvaluationWorkflowService
            .GetFraudEvaluationDetailsAsync(transactionId);

        if (evaluation is null)
        {
            return NotFound(new
            {
                message = "Fraud evaluation was not found."
            });
        }

        return Ok(evaluation);
    }

}