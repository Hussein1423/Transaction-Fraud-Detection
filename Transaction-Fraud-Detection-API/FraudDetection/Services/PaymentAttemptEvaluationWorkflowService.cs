using Microsoft.EntityFrameworkCore;
using Transaction_Fraud_Detection_API.Data;
using Transaction_Fraud_Detection_API.DTOs;
using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection;
using Transaction_Fraud_Detection_API.FraudDetection.Interfaces;
using Transaction_Fraud_Detection_API.Models;

namespace Transaction_Fraud_Detection_API.FraudDetection.Services;

public class PaymentAttemptEvaluationWorkflowService : IPaymentAttemptEvaluationWorkflowService
{
    private readonly AppDbContext _context;
    private readonly IPaymentAttemptFraudEvaluationService _fraudEvaluationService;
    public PaymentAttemptEvaluationWorkflowService(
        AppDbContext context,
        IPaymentAttemptFraudEvaluationService fraudEvaluationService)
    {
        _context = context;
        _fraudEvaluationService = fraudEvaluationService;
    }

    public async Task<CombinedFraudEvaluationResultDto> RecordPaymentAttemptAsync(Guid transactionId,PaymentAttemptRequestDto request)
    {
        // 1. Retrieve the transaction.
        Transaction transaction = await GetTransactionAsync(transactionId);

        // 2. Create and save the payment attempt.
        PaymentAttempt paymentAttempt =
            await CreatePaymentAttemptAsync(transaction, request);

        // 3. Retrieve relevant previous attempts.
        List<PaymentAttempt> previousAttempts =
            await GetPreviousPaymentAttemptsAsync(transaction, paymentAttempt);

        // 4. Build the evaluation context.
        PaymentAttemptEvaluationContext evaluationContext =
            new PaymentAttemptEvaluationContext
            {
                CurrentPaymentAttempt = paymentAttempt,
                PreviousPaymentAttempts = previousAttempts
            };

        // 5. Evaluate payment-attempt fraud rules.
        var evaluationResult = _fraudEvaluationService.Evaluate(evaluationContext);

        // 6. Persist the payment-attempt evaluation.
        await SaveEvaluationAsync(paymentAttempt, evaluationResult);

        // 7. Retrieve both persisted evaluation results.
        var transactionResult =
            await GetTransactionFraudResultAsync(transactionId);

        var paymentAttemptResult =
            await GetPaymentAttemptFraudResultAsync(paymentAttempt);

        // 8. Return the combined result.
        return CreateCombinedResult(transactionResult, paymentAttemptResult);
    }
    private async Task<TransactionFraudResultDto> GetTransactionFraudResultAsync(Guid transactionId)
    {
        var evaluation = await _context.FraudEvaluations
            .FirstOrDefaultAsync(e => e.TransactionId == transactionId);

        if (evaluation is null)
        {
            throw new InvalidOperationException(
                "Fraud evaluation for this transaction was not found.");
        }

        var triggeredRules = await _context.TriggeredRules
            .Where(r => r.FraudEvaluationId == evaluation.FraudEvaluationId)
            .Select(r => new TriggeredRuleResultDto
            {
                RuleCode = r.RuleCode,
                PointsAwarded = r.PointsAwarded
            })
            .ToListAsync();

        return new TransactionFraudResultDto
        {
            TransactionId = evaluation.TransactionId,
            RiskScore = evaluation.RiskScore,
            TriggeredRules = triggeredRules
        };
    }


    private async Task<PaymentAttemptFraudResultDto> GetPaymentAttemptFraudResultAsync(PaymentAttempt paymentAttempt)
    {
        var evaluation = await _context.PaymentAttemptFraudEvaluations
            .FirstOrDefaultAsync(e => e.PaymentAttemptId == paymentAttempt.PaymentAttemptId);

        if (evaluation is null)
        {
            return new PaymentAttemptFraudResultDto
            {
                PaymentAttemptId = paymentAttempt.PaymentAttemptId,
                Status = paymentAttempt.Status,
                RiskScore = null,
                TriggeredRules = new()
            };
        }

        var triggeredRules = await _context.PaymentAttemptTriggeredRules
            .Where(r => r.PaymentAttemptFraudEvaluationId ==
                        evaluation.PaymentAttemptFraudEvaluationId)
            .Select(r => new TriggeredRuleResultDto
            {
                RuleCode = r.RuleCode,
                PointsAwarded = r.PointsAwarded
            })
            .ToListAsync();

        return new PaymentAttemptFraudResultDto
        {
            PaymentAttemptId = paymentAttempt.PaymentAttemptId,
            Status = paymentAttempt.Status,
            RiskScore = evaluation.RiskScore,
            TriggeredRules = triggeredRules
        };
    }

    private CombinedFraudEvaluationResultDto CreateCombinedResult(TransactionFraudResultDto transactionResult,PaymentAttemptFraudResultDto paymentAttemptResult)
    {
        var combinedTriggeredRules = new List<CombinedTriggeredRuleResultDto>();

        combinedTriggeredRules.AddRange(
            transactionResult.TriggeredRules.Select(rule =>
                new CombinedTriggeredRuleResultDto
                {
                    RuleCode = rule.RuleCode,
                    PointsAwarded = rule.PointsAwarded,
                    Source = FraudEvaluationSource.Transaction
                }));

        combinedTriggeredRules.AddRange(
            paymentAttemptResult.TriggeredRules.Select(rule =>
                new CombinedTriggeredRuleResultDto
                {
                    RuleCode = rule.RuleCode,
                    PointsAwarded = rule.PointsAwarded,
                    Source = FraudEvaluationSource.PaymentAttempt
                }));

        int totalRiskScore =
            transactionResult.RiskScore + (paymentAttemptResult.RiskScore ?? 0);

        var finalDecision = totalRiskScore >= 50
            ? FraudDecision.FlagForReview
            : FraudDecision.Allow;

        return new CombinedFraudEvaluationResultDto
        {
            Transaction = transactionResult,
            PaymentAttempt = paymentAttemptResult,
            TotalRiskScore = totalRiskScore,
            CombinedTriggeredRules = combinedTriggeredRules,
            FinalDecision = finalDecision
        };
    }
    private async Task<Transaction> GetTransactionAsync(Guid transactionId)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

        if (transaction is null)
        {
            throw new KeyNotFoundException(
                $"Transaction with ID '{transactionId}' was not found.");
        }

        return transaction;
    }

    private async Task<PaymentAttempt> CreatePaymentAttemptAsync(Transaction transaction, PaymentAttemptRequestDto request)
    {
        SimulatedPaymentResult simulatedPaymentResult = SimulatePayment(transaction);
        var paymentAttempt = new PaymentAttempt
        {
            TransactionId = transaction.TransactionId,
            Transaction = transaction,
            Amount = transaction.Amount,
            PaymentInstrumentFingerprint = request.PaymentInstrumentFingerprint,
            Currency = transaction.Currency,
            Status = simulatedPaymentResult.Status,
            FailureReason = simulatedPaymentResult.FailureReason,
            AttemptedAt = DateTimeOffset.UtcNow
        };

        _context.PaymentAttempts.Add(paymentAttempt);

        await _context.SaveChangesAsync();

        return paymentAttempt;
    }

    private async Task<List<PaymentAttempt>> GetPreviousPaymentAttemptsAsync(Transaction transaction,PaymentAttempt currentPaymentAttempt)
    {
        // Find the latest previous successful attempt.
        var lastSuccessfulAttemptAt = await _context.PaymentAttempts
            .Where(p => p.Transaction.UserId == transaction.UserId)
            .Where(p => p.AttemptedAt < currentPaymentAttempt.AttemptedAt)
            .Where(p => p.Status == PaymentAttemptStatus.Succeeded)
            .Select(p => (DateTimeOffset?)p.AttemptedAt)
            .MaxAsync();

        // Define the 30-minute window.
        var windowStart = currentPaymentAttempt.AttemptedAt.AddMinutes(-30);

        // Retrieve failed attempts within the relevant window.
        var previousAttemptsQuery = _context.PaymentAttempts
            .Include(p => p.Transaction)
            .Where(p => p.Transaction.UserId == transaction.UserId)
            .Where(p => p.AttemptedAt >= windowStart)
            .Where(p => p.AttemptedAt < currentPaymentAttempt.AttemptedAt)
            .Where(p => p.Status == PaymentAttemptStatus.Failed);

        // Ignore failures that occurred before the latest success.
        if (lastSuccessfulAttemptAt.HasValue)
        {
            previousAttemptsQuery = previousAttemptsQuery
                .Where(p => p.AttemptedAt > lastSuccessfulAttemptAt.Value);
        }

        return await previousAttemptsQuery.ToListAsync();
    }
    private bool IsSupportedCurrency(string currency)
    {
        string[] supportedCurrencies = { "SAR", "USD", "YER", "JOD" };

        return supportedCurrencies.Contains(
            currency,
            StringComparer.OrdinalIgnoreCase);
    }

    private bool IsAmountWithinLimits(decimal amount)
    {
        return amount >= 50m && amount <= 5500m;
    }

    private SimulatedPaymentResult SimulatePayment(Transaction transaction)
    {
        if (!IsSupportedCurrency(transaction.Currency))
        {
            return new SimulatedPaymentResult(
                PaymentAttemptStatus.Failed,
                "Unsupported currency");
        }

        if (!IsAmountWithinLimits(transaction.Amount))
        {
            return new SimulatedPaymentResult(
                PaymentAttemptStatus.Failed,
                "Amount out of limits");
        }

        return new SimulatedPaymentResult(
            PaymentAttemptStatus.Succeeded,
            null);
    }
  

    private async Task SaveEvaluationAsync(
     PaymentAttempt paymentAttempt,
     FraudEvaluationResultDto evaluationResult)
    {
        var evaluation = new PaymentAttemptFraudEvaluation
        {
            PaymentAttemptId = paymentAttempt.PaymentAttemptId,
            RiskScore = evaluationResult.RiskScore,
            Decision = evaluationResult.Decision,
            EvaluatedAt = DateTimeOffset.UtcNow
        };

        _context.PaymentAttemptFraudEvaluations.Add(evaluation);

        await _context.SaveChangesAsync();

        // Map evaluation results to database entities.
        var triggeredRules = evaluationResult.TriggeredRules
            .Select(rule => new PaymentAttemptTriggeredRule
            {
                PaymentAttemptFraudEvaluationId =
                    evaluation.PaymentAttemptFraudEvaluationId,

                RuleCode = rule.RuleCode,
                PointsAwarded = rule.PointsAwarded
            })
            .ToList();

        if (triggeredRules.Count > 0)
        {
            _context.PaymentAttemptTriggeredRules.AddRange(triggeredRules);

            await _context.SaveChangesAsync();
        }
    }

    private PaymentAttemptProcessingResultDto CreateResult(PaymentAttempt paymentAttempt,FraudEvaluationResultDto? evaluationResult = null)
    {
        return new PaymentAttemptProcessingResultDto
        {
            PaymentAttemptId = paymentAttempt.PaymentAttemptId,
            Status = paymentAttempt.Status,
            FailureReason = paymentAttempt.FailureReason,
            RiskScore = evaluationResult?.RiskScore,
            Decision = evaluationResult?.Decision
        };
    }

    private sealed record SimulatedPaymentResult(PaymentAttemptStatus Status,string? FailureReason);
}
