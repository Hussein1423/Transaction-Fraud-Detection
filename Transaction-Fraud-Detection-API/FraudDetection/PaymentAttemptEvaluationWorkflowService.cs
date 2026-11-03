
using Microsoft.EntityFrameworkCore;
using Transaction_Fraud_Detection_API.Data;
using Transaction_Fraud_Detection_API.DTOs;
using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection;
using Transaction_Fraud_Detection_API.Models;

namespace Transaction_Fraud_Detection_API;

public class PaymentAttemptEvaluationWorkflowService
    : IPaymentAttemptEvaluationWorkflowService
{
    private readonly AppDbContext _context;
    private readonly IEnumerable<IPaymentAttemptFraudRule> _fraudRules;

    public PaymentAttemptEvaluationWorkflowService(
        AppDbContext context,
        IEnumerable<IPaymentAttemptFraudRule> fraudRules)
    {
        _context = context;
        _fraudRules = fraudRules;
    }

    public async Task<PaymentAttemptProcessingResultDto> RecordPaymentAttemptAsync(
        Guid transactionId)
    {
        // 1. Retrieve the transaction.
        var transaction = await GetTransactionAsync(transactionId);

        // 2. Create and save the payment attempt.
        var paymentAttempt = await CreatePaymentAttemptAsync(
            transaction);

        // 3. Failed attempts are recorded but not evaluated.
        if (paymentAttempt.Status == PaymentAttemptStatus.Failed)
        {
            return CreateResult(paymentAttempt);
        }

        // 4. Retrieve relevant previous attempts.
        var previousAttempts = await GetPreviousPaymentAttemptsAsync(
            transaction,
            paymentAttempt);

        // 5. Build the evaluation context.
        var evaluationContext = new PaymentAttemptEvaluationContext
        {
            CurrentPaymentAttempt = paymentAttempt,
            PreviousPaymentAttempts = previousAttempts
        };

        // 6. Evaluate fraud rules.
        var evaluationResult = EvaluatePaymentAttempt(evaluationContext);

        // 7. Persist the evaluation and triggered rules.
        await SaveEvaluationAsync(paymentAttempt, evaluationResult);

        // 8. Return the result.
        return CreateResult(paymentAttempt, evaluationResult);
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

    private async Task<PaymentAttempt> CreatePaymentAttemptAsync(Transaction transaction)
    {
        SimulatedPaymentResult simulatedPaymentResult = SimulatePayment(transaction);
        var paymentAttempt = new PaymentAttempt
        {
            TransactionId = transaction.TransactionId,
            Transaction = transaction,
            Status = simulatedPaymentResult.Status,
            FailureReason = simulatedPaymentResult.FailureReason,
            AttemptedAt = DateTimeOffset.UtcNow
        };

        _context.PaymentAttempts.Add(paymentAttempt);

        await _context.SaveChangesAsync();

        return paymentAttempt;
    }

    private async Task<List<PaymentAttempt>> GetPreviousPaymentAttemptsAsync(
        Transaction transaction,
        PaymentAttempt currentPaymentAttempt)
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
    private PaymentAttemptEvaluationOutcome EvaluatePaymentAttempt(
        PaymentAttemptEvaluationContext context)
    {
        int riskScore = 0;

        var triggeredRules = new List<PaymentAttemptTriggeredRule>();

        foreach (var rule in _fraudRules)
        {
            var result = rule.Evaluate(context);

            if (!result.IsTriggered)
                continue;

            riskScore = Math.Min(100, riskScore + result.PointsAwarded);

            triggeredRules.Add(new PaymentAttemptTriggeredRule
            {
                RuleCode = rule.RuleCode,
                PointsAwarded = result.PointsAwarded
            });
        }

        var decision = riskScore >= 50
            ? FraudDecision.FlagForReview
            : FraudDecision.Allow;

        return new PaymentAttemptEvaluationOutcome(
            riskScore,
            decision,
            triggeredRules);
    }

    private async Task SaveEvaluationAsync(
        PaymentAttempt paymentAttempt,
        PaymentAttemptEvaluationOutcome evaluationResult)
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

        // Associate triggered rules with the saved evaluation.
        foreach (var triggeredRule in evaluationResult.TriggeredRules)
        {
            triggeredRule.PaymentAttemptFraudEvaluationId =
                evaluation.PaymentAttemptFraudEvaluationId;
        }

        if (evaluationResult.TriggeredRules.Count > 0)
        {
            _context.PaymentAttemptTriggeredRules.AddRange(
                evaluationResult.TriggeredRules);

            await _context.SaveChangesAsync();
        }
    }

    private PaymentAttemptProcessingResultDto CreateResult(
        PaymentAttempt paymentAttempt,
        PaymentAttemptEvaluationOutcome? evaluationResult = null)
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

    private sealed record PaymentAttemptEvaluationOutcome(
        int RiskScore,
        FraudDecision Decision,
        List<PaymentAttemptTriggeredRule> TriggeredRules);

    private sealed record SimulatedPaymentResult(
    PaymentAttemptStatus Status,
    string? FailureReason);
}
