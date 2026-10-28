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
    [ProducesResponseType(typeof(List<TransactionEvaluationDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFlaggedTransactions([FromQuery] int pageNumber = 1,[FromQuery] int pageSize = 10)
    {
        const int maxPageSize = 100;

        if (pageNumber < 1)
        {
            return BadRequest(new
            {
                message = "Page number must be greater than 0."
            });
        }

        if (pageSize < 1 || pageSize > maxPageSize)
        {
            return BadRequest(new
            {
                message = $"Page size must be between 1 and {maxPageSize}."
            });
        }

        var transactions = await _transactionEvaluationWorkflowService
            .GetFlaggedTransactionsAsync(pageNumber, pageSize);

        if (!transactions.Any())
        {
            return NotFound(new
            {
                message = "No flagged transactions found on this page."
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