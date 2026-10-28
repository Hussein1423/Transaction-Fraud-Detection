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


    public async Task<TransactionEvaluationDetailsDto?> GetTransactionWithEvaluationAsync(Guid transactionId)
    {
        return await _dbContext.Transactions
            .Where(t => t.TransactionId == transactionId)
            .Join(
                _dbContext.FraudEvaluations,
                transaction => transaction.TransactionId,
                evaluation => evaluation.TransactionId,
                (transaction, evaluation) => new TransactionEvaluationDetailsDto
                {
                    TransactionId = transaction.TransactionId,
                    UserId = transaction.UserId,
                    Amount = transaction.Amount,
                    Currency = transaction.Currency,
                    MerchantId = transaction.MerchantId,
                    Timestamp = transaction.Timestamp,
                    RiskScore = evaluation.RiskScore,
                    Decision = evaluation.Decision,
                    EvaluatedAt = evaluation.EvaluatedAt
                })
            .FirstOrDefaultAsync();
    }


    public async Task<List<TransactionEvaluationDetailsDto>> GetFlaggedTransactionsAsync(
        int pageNumber = 1,
        int pageSize = 10)
    {
        return await _dbContext.Transactions
            .Join(
                _dbContext.FraudEvaluations,
                transaction => transaction.TransactionId,
                evaluation => evaluation.TransactionId,
                (transaction, evaluation) => new { transaction, evaluation })
            .Where(x => x.evaluation.Decision == Enums.FraudDecision.FlagForReview)
            .OrderByDescending(x => x.evaluation.EvaluatedAt)
            .ThenBy(x => x.transaction.TransactionId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TransactionEvaluationDetailsDto
            {
                TransactionId = x.transaction.TransactionId,
                UserId = x.transaction.UserId,
                Amount = x.transaction.Amount,
                Currency = x.transaction.Currency,
                MerchantId = x.transaction.MerchantId,
                Timestamp = x.transaction.Timestamp,
                RiskScore = x.evaluation.RiskScore,
                Decision = x.evaluation.Decision,
                EvaluatedAt = x.evaluation.EvaluatedAt
            })
            .ToListAsync();
    }


    public async Task<FraudEvaluationDetailsDto?> GetFraudEvaluationDetailsAsync(Guid transactionId)
    {
        var evaluation = await _dbContext.FraudEvaluations
            .Where(e => e.TransactionId == transactionId)
            .Select(e => new FraudEvaluationDetailsDto
            {
                TransactionId = e.TransactionId,
                RiskScore = e.RiskScore,
                Decision = e.Decision,
                EvaluatedAt = e.EvaluatedAt,
                TriggeredRules = e.FraudEvaluationId == Guid.Empty
                    ? new List<TriggeredRuleResultDto>()
                    : _dbContext.TriggeredRules
                        .Where(r => r.FraudEvaluationId == e.FraudEvaluationId)
                        .Select(r => new TriggeredRuleResultDto
                        {
                            RuleCode = r.RuleCode,
                            PointsAwarded = r.PointsAwarded
                        })
                        .ToList()
            })
            .FirstOrDefaultAsync();

        return evaluation;
    }
    public async Task<FraudEvaluationResultDto> ProcessTransactionAsync(CreateTransactionRequestDto request)
    {
        await using var dbTransaction =  await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var transaction = CreateTransaction(request);

            await SaveTransactionAsync(transaction);

            var context = await BuildContextAsync(transaction);

            var result = EvaluateTransaction(context);

            var evaluation = await SaveEvaluationAsync(transaction, result);

            await SaveTriggeredRulesAsync(evaluation, result);

            await dbTransaction.CommitAsync();

            return result;
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }
    }
}