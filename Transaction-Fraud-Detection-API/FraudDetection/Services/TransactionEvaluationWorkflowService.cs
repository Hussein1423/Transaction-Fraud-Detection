using Microsoft.EntityFrameworkCore;
using System.Net;
using Transaction_Fraud_Detection_API.Data;
using Transaction_Fraud_Detection_API.DTOs;
using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection.Interfaces;
using Transaction_Fraud_Detection_API.Interfaces;
using Transaction_Fraud_Detection_API.Models;

namespace Transaction_Fraud_Detection_API.FraudDetection.Services;

public class TransactionEvaluationWorkflowService
{
    private readonly AppDbContext _dbContext;
    private readonly IFraudEvaluationService _fraudEvaluationService;
    private readonly ITransactionLocationService _transactionLocationService;

    public TransactionEvaluationWorkflowService(AppDbContext dbContext,IFraudEvaluationService fraudEvaluationService, ITransactionLocationService transactionLocationService)
    {
        _dbContext = dbContext;
        _fraudEvaluationService = fraudEvaluationService;
        _transactionLocationService = transactionLocationService;
    }

    private Transaction CreateTransaction(CreateTransactionRequestDto request,TransactionLocation? location)
    {
        return new Transaction
        {
            UserId = request.UserId,
            Amount = request.Amount,
            Currency = request.Currency,
            MerchantId = request.MerchantId,
            Timestamp = DateTimeOffset.UtcNow,
            City = location?.City,
            Country = location?.Country
        };
    }

    private async Task<Transaction> SaveTransactionAsync(Transaction transaction)
    {
        _dbContext.Transactions.Add(transaction);
        await _dbContext.SaveChangesAsync();
        return transaction;
    }

    private async Task<List<Transaction>> GetPreviousTransactionsAsync(Transaction transaction)
    {
        var twentyFourHoursAgo = transaction.Timestamp.AddHours(-24);

        return await _dbContext.Transactions
            .Where(t => t.UserId == transaction.UserId
                        && t.Timestamp >= twentyFourHoursAgo
                        && t.Timestamp <= transaction.Timestamp
                        && t.TransactionId != transaction.TransactionId)
            .ToListAsync();
    }

    private async Task<FraudEvaluationContext> BuildContextAsync(Transaction transaction)
    {
        var previousTransactions = await GetPreviousTransactionsAsync(transaction);
        var recentAccountChangeEvents = await GetRecentAccountChangeEventsAsync(transaction);

        return new FraudEvaluationContext
        {
            CurrentTransaction = transaction,
            PreviousTransactions = previousTransactions,
            RecentAccountChangeEvents = recentAccountChangeEvents
        };
    }

    private FraudEvaluationResultDto EvaluateTransaction(FraudEvaluationContext context)
    {
        return _fraudEvaluationService.Evaluate(context);
    }

    private async Task<List<(AccountChangeType Type, DateTimeOffset Time)>> GetRecentAccountChangeEventsAsync(Transaction transaction)
    {
        var oneHourAgo = transaction.Timestamp.AddHours(-1);

        var events = await _dbContext.AccountChangeEvents
            .AsNoTracking()
            .Where(e =>
                e.UserId == transaction.UserId &&
                e.Time >= oneHourAgo &&
                e.Time < transaction.Timestamp)
            .Select(e => new
            {
                e.Type,
                e.Time
            })
            .ToListAsync();

        return events
            .Select(e => (e.Type, e.Time))
            .ToList();
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
    public async Task<FraudEvaluationResultDto> ProcessTransactionAsync(CreateTransactionRequestDto request, IPAddress? ipAddress)
    {
        await using var dbTransaction =  await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var location = _transactionLocationService.Resolve(ipAddress);

            var transaction = CreateTransaction(request, location);

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

    public async Task<FraudEvaluationResultDto> UpdateTransactionAsync(
    Guid transactionId,
    UpdateTransactionRequestDto request)
    {
        await using var dbTransaction =
            await _dbContext.Database.BeginTransactionAsync();

        try
        {
            var transaction = await GetTransactionForUpdateAsync(transactionId);

            await EnsureTransactionCanBeUpdatedAsync(transactionId);

            var evaluation = await GetFraudEvaluationForUpdateAsync(transactionId);

            ApplyTransactionUpdates(transaction, request);

            var context = await BuildContextAsync(transaction);

            var result = EvaluateTransaction(context);

            UpdateFraudEvaluation(evaluation, result);

            await ReplaceTriggeredRulesAsync(evaluation, result);

            await _dbContext.SaveChangesAsync();

            await dbTransaction.CommitAsync();

            return result;
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }
    }

    private async Task<Transaction> GetTransactionForUpdateAsync(
    Guid transactionId)
    {
        var transaction = await _dbContext.Transactions
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

        if (transaction is null)
        {
            throw new KeyNotFoundException("Transaction was not found.");
        }

        return transaction;
    }
    private async Task EnsureTransactionCanBeUpdatedAsync(
    Guid transactionId)
    {
        var hasSuccessfulAttempt = await _dbContext.PaymentAttempts
            .AnyAsync(p =>
                p.TransactionId == transactionId &&
                p.Status == PaymentAttemptStatus.Succeeded);

        if (hasSuccessfulAttempt)
        {
            throw new InvalidOperationException(
                "A transaction cannot be updated after a successful payment attempt.");
        }
    }
    private async Task<FraudEvaluation> GetFraudEvaluationForUpdateAsync(
    Guid transactionId)
    {
        var evaluation = await _dbContext.FraudEvaluations
            .FirstOrDefaultAsync(e => e.TransactionId == transactionId);

        if (evaluation is null)
        {
            throw new KeyNotFoundException(
                "Fraud evaluation for this transaction was not found.");
        }

        return evaluation;
    }

    private void ApplyTransactionUpdates(
    Transaction transaction,
    UpdateTransactionRequestDto request)
    {
        if (request.Amount.HasValue)
        {
            transaction.Amount = request.Amount.Value;
        }

        if (request.Currency is not null)
        {
            transaction.Currency = request.Currency;
        }

        transaction.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void UpdateFraudEvaluation(
    FraudEvaluation evaluation,
    FraudEvaluationResultDto result)
    {
        evaluation.RiskScore = result.RiskScore;
        evaluation.Decision = result.Decision;
        evaluation.EvaluatedAt = DateTimeOffset.UtcNow;
    }

    private async Task ReplaceTriggeredRulesAsync(
    FraudEvaluation evaluation,
    FraudEvaluationResultDto result)
    {
        await _dbContext.TriggeredRules
            .Where(r => r.FraudEvaluationId == evaluation.FraudEvaluationId)
            .ExecuteDeleteAsync();

        var triggeredRules = result.TriggeredRules.Select(rule =>
            new TriggeredRule
            {
                FraudEvaluationId = evaluation.FraudEvaluationId,
                RuleCode = rule.RuleCode,
                PointsAwarded = rule.PointsAwarded
            });

        _dbContext.TriggeredRules.AddRange(triggeredRules);
    }
}