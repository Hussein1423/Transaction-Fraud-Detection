using Microsoft.EntityFrameworkCore;
using Transaction_Fraud_Detection_API.Data;
using Transaction_Fraud_Detection_API.DTOs;
using Transaction_Fraud_Detection_API.Models;

namespace Transaction_Fraud_Detection_API.FraudDetection;

public class TransactionEvaluationWorkflowService
{
    private readonly AppDbContext _dbContext;
    private readonly IFraudEvaluationService _fraudEvaluationService;

    public TransactionEvaluationWorkflowService(AppDbContext dbContext,IFraudEvaluationService fraudEvaluationService)
    {
        _dbContext = dbContext;
        _fraudEvaluationService = fraudEvaluationService;
    }

    private Transaction CreateTransaction(CreateTransactionRequestDto request)
    {
        var transaction = new Transaction
        {
            UserId = request.UserId,
            Amount = request.Amount,
            Currency = request.Currency,
            MerchantId = request.MerchantId,
            Timestamp = DateTimeOffset.UtcNow
        };
        return transaction;
    }

    private async Task<Transaction> SaveTransactionAsync(Transaction transaction)
    {
        _dbContext.Transactions.Add(transaction);
        await _dbContext.SaveChangesAsync();
        return transaction;
    }

    private async Task<List<Transaction>> GetPreviousTransactionsAsync(Transaction transaction)
    {
        var tenMinutesAgo = transaction.Timestamp.AddMinutes(-10);

        return await _dbContext.Transactions
            .Where(t => t.UserId == transaction.UserId
                        && t.Timestamp >= tenMinutesAgo
                        && t.Timestamp <= transaction.Timestamp
                        && t.TransactionId != transaction.TransactionId)
            .ToListAsync();
    }

    private async Task<FraudEvaluationContext> BuildContextAsync(Transaction transaction)
    {
        var previousTransactions = await GetPreviousTransactionsAsync(transaction);

        return new FraudEvaluationContext
        {
            CurrentTransaction = transaction,
            PreviousTransactions = previousTransactions
        };
    }

    private FraudEvaluationResultDto EvaluateTransaction(FraudEvaluationContext context)
    {
        return _fraudEvaluationService.Evaluate(context);
    }

    private async Task<FraudEvaluation> SaveEvaluationAsync(Transaction transaction,FraudEvaluationResultDto result)
    {
        var evaluation = new FraudEvaluation
        {
            TransactionId = transaction.TransactionId,
            RiskScore = result.RiskScore,
            Decision = result.Decision,
            EvaluatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.FraudEvaluations.Add(evaluation);
        await _dbContext.SaveChangesAsync();

        return evaluation;
    }


    private async Task SaveTriggeredRulesAsync(FraudEvaluation evaluation,FraudEvaluationResultDto result)
    {
        var triggeredRules = result.TriggeredRules.Select(rule =>
            new TriggeredRule
            {
                FraudEvaluationId = evaluation.FraudEvaluationId,
                RuleCode = rule.RuleCode,
                PointsAwarded = rule.PointsAwarded
            });

        _dbContext.TriggeredRules.AddRange(triggeredRules);
        await _dbContext.SaveChangesAsync();
    }


    public async Task<FraudEvaluationResultDto> ProcessTransactionAsync( CreateTransactionRequestDto request)
    {
        var transaction = CreateTransaction(request);

        await SaveTransactionAsync(transaction);

        var context = await BuildContextAsync(transaction);

        var result = EvaluateTransaction(context);

        var evaluation = await SaveEvaluationAsync(transaction, result);

        await SaveTriggeredRulesAsync(evaluation, result);

        return result;
    }
}