
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Transaction_Fraud_Detection_API.DTOs;
using Transaction_Fraud_Detection_API.FraudDetection.Interfaces;
using Transaction_Fraud_Detection_API.FraudDetection.Services;

namespace Transaction_Fraud_Detection_API.Controllers;

[ApiController]
[Route("api/Transactions")]
public class TransactionsController : ControllerBase
{
    private readonly TransactionEvaluationWorkflowService
        _transactionEvaluationWorkflowService;

    private readonly IPaymentAttemptEvaluationWorkflowService
        _paymentAttemptEvaluationWorkflowService;

    public TransactionsController(
        TransactionEvaluationWorkflowService transactionEvaluationWorkflowService,
        IPaymentAttemptEvaluationWorkflowService paymentAttemptEvaluationWorkflowService)
    {
        _transactionEvaluationWorkflowService =
            transactionEvaluationWorkflowService;

        _paymentAttemptEvaluationWorkflowService =
            paymentAttemptEvaluationWorkflowService;
    }

    [HttpPost("v1")]
    [ProducesResponseType(typeof(FraudEvaluationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequestDto request)
    {
        //var ipAddress = IPAddress.Parse("8.8.8.8");
        var ipAddress = HttpContext.Connection.RemoteIpAddress;
        var result = await _transactionEvaluationWorkflowService.ProcessTransactionAsync(request, ipAddress);

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
            });
        }

        return Ok(transaction);
    }

    [HttpGet("flagged/v1")]
    [ProducesResponseType(typeof(List<TransactionEvaluationDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFlaggedTransactions(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
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

    // Record a new payment attempt for an existing transaction.
    [HttpPost("{transactionId:guid}/payment-attempts/v1")]
    [ProducesResponseType(typeof(CombinedFraudEvaluationResultDto),StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordPaymentAttempt(Guid transactionId, [FromBody] PaymentAttemptRequestDto request)
    {
        try
        {
            var result = await _paymentAttemptEvaluationWorkflowService.RecordPaymentAttemptAsync(transactionId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                message = exception.Message
            });
        }
    }

    [HttpPatch("{transactionId:guid}/v1")]
    [ProducesResponseType(typeof(FraudEvaluationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTransaction(
    Guid transactionId,
    [FromBody] UpdateTransactionRequestDto request)
    {
        try
        {
            var result = await _transactionEvaluationWorkflowService
                .UpdateTransactionAsync(transactionId, request);

            return Ok(result);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                message = exception.Message
            });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new
            {
                message = exception.Message
            });
        }
    }
}
