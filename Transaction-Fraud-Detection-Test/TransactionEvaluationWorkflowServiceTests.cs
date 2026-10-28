
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Transaction_Fraud_Detection_API.Data;
using Transaction_Fraud_Detection_API.DTOs;
using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection;
using Transaction_Fraud_Detection_API.Models;
using Xunit;

namespace Transaction_Fraud_Detection_Test.FraudDetection;

public class TransactionEvaluationWorkflowServiceTests
{
    [Fact]
    public async Task ProcessTransactionAsync_ValidRequest_SavesTransactionAndEvaluation()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var fraudEvaluationService = new FakeFraudEvaluationService(
            new FraudEvaluationResultDto
            {
                RiskScore = 30,
                Decision = FraudDecision.Allow
            });

        var workflow = new TransactionEvaluationWorkflowService(
            dbContext,
            fraudEvaluationService);

        var request = CreateRequest();

        // Act
        var result = await workflow.ProcessTransactionAsync(request);

        // Assert
        var savedTransaction = await dbContext.Transactions.SingleAsync();
        var savedEvaluation = await dbContext.FraudEvaluations.SingleAsync();

        Assert.Equal(request.UserId, savedTransaction.UserId);
        Assert.Equal(request.Amount, savedTransaction.Amount);
        Assert.Equal(request.Currency, savedTransaction.Currency);
        Assert.Equal(request.MerchantId, savedTransaction.MerchantId);
        Assert.NotEqual(Guid.Empty, savedTransaction.TransactionId);

        Assert.Equal(savedTransaction.TransactionId, savedEvaluation.TransactionId);
        Assert.Equal(30, savedEvaluation.RiskScore);
        Assert.Equal(FraudDecision.Allow, savedEvaluation.Decision);

        Assert.Equal(30, result.RiskScore);
        Assert.Equal(FraudDecision.Allow, result.Decision);
    }

    [Fact]
    public async Task ProcessTransactionAsync_PassesPreviousTransactionsToEvaluation()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        var previousTransaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            UserId = "user-1",
            Amount = 100,
            Currency = "SAR",
            MerchantId = "merchant-1",
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        dbContext.Transactions.Add(previousTransaction);
        await dbContext.SaveChangesAsync();

        var fraudEvaluationService = new FakeFraudEvaluationService(
            new FraudEvaluationResultDto
            {
                RiskScore = 50,
                Decision = FraudDecision.FlagForReview
            });

        var workflow = new TransactionEvaluationWorkflowService(
            dbContext,
            fraudEvaluationService);

        var request = CreateRequest();

        // Act
        await workflow.ProcessTransactionAsync(request);

        // Assert
        Assert.NotNull(fraudEvaluationService.ReceivedContext);

        Assert.Contains(
            fraudEvaluationService.ReceivedContext.PreviousTransactions,
            transaction => transaction.TransactionId == previousTransaction.TransactionId);

        Assert.Equal(
            "user-1",
            fraudEvaluationService.ReceivedContext.CurrentTransaction.UserId);
    }

    [Fact]
    public async Task ProcessTransactionAsync_WhenEvaluationFlagsTransaction_SavesFlaggedDecision()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        var fraudEvaluationService = new FakeFraudEvaluationService(
            new FraudEvaluationResultDto
            {
                RiskScore = 50,
                Decision = FraudDecision.FlagForReview
            });

        var workflow = new TransactionEvaluationWorkflowService(
            dbContext,
            fraudEvaluationService);

        // Act
        var result = await workflow.ProcessTransactionAsync(CreateRequest());

        // Assert
        var savedEvaluation = await dbContext.FraudEvaluations.SingleAsync();

        Assert.Equal(50, result.RiskScore);
        Assert.Equal(FraudDecision.FlagForReview, result.Decision);
        Assert.Equal(FraudDecision.FlagForReview, savedEvaluation.Decision);
    }


    [Fact]
    public async Task ProcessTransactionAsync_WhenRulesTrigger_SavesTriggeredRules()
    {
        await using var dbContext = CreateDbContext();

        var fraudEvaluationService = new FakeFraudEvaluationService(
            new FraudEvaluationResultDto
            {
                RiskScore = 70,
                Decision = FraudDecision.FlagForReview,
                TriggeredRules = new List<TriggeredRuleResultDto>
                {
                new()
                {
                    RuleCode = FraudRuleCode.HighAmount,
                    PointsAwarded = 30
                },
                new()
                {
                    RuleCode = FraudRuleCode.HighVelocity,
                    PointsAwarded = 40
                }
                }
            });

        var workflow = new TransactionEvaluationWorkflowService(
            dbContext,
            fraudEvaluationService);

        await workflow.ProcessTransactionAsync(CreateRequest());

        var evaluation = await dbContext.FraudEvaluations.SingleAsync();
        var triggeredRules = await dbContext.TriggeredRules
            .OrderBy(rule => rule.RuleCode)
            .ToListAsync();

        Assert.Equal(2, triggeredRules.Count);

        Assert.All(
            triggeredRules,
            rule => Assert.Equal(evaluation.FraudEvaluationId, rule.FraudEvaluationId));

        Assert.Contains(triggeredRules, rule =>
            rule.RuleCode == FraudRuleCode.HighAmount &&
            rule.PointsAwarded == 30);

        Assert.Contains(triggeredRules, rule =>
            rule.RuleCode == FraudRuleCode.HighVelocity &&
            rule.PointsAwarded == 40);
    }



    [Fact]
    public async Task GetFlaggedTransactionsAsync_ReturnsOnlyFlaggedTransactionsInDescendingOrder()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        var now = DateTimeOffset.UtcNow;

        var olderTransaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            UserId = "user-1",
            Amount = 200,
            Currency = "SAR",
            MerchantId = "merchant-1",
            Timestamp = now.AddMinutes(-10)
        };

        var newerTransaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            UserId = "user-2",
            Amount = 1500,
            Currency = "SAR",
            MerchantId = "merchant-2",
            Timestamp = now.AddMinutes(-5)
        };

        var allowedTransaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            UserId = "user-3",
            Amount = 100,
            Currency = "SAR",
            MerchantId = "merchant-3",
            Timestamp = now
        };

        dbContext.Transactions.AddRange(
            olderTransaction,
            newerTransaction,
            allowedTransaction);

        await dbContext.SaveChangesAsync();

        dbContext.FraudEvaluations.AddRange(
            new FraudEvaluation
            {
                FraudEvaluationId = Guid.NewGuid(),
                TransactionId = olderTransaction.TransactionId,
                RiskScore = 60,
                Decision = FraudDecision.FlagForReview,
                EvaluatedAt = now.AddMinutes(-10)
            },
            new FraudEvaluation
            {
                FraudEvaluationId = Guid.NewGuid(),
                TransactionId = newerTransaction.TransactionId,
                RiskScore = 80,
                Decision = FraudDecision.FlagForReview,
                EvaluatedAt = now.AddMinutes(-5)
            },
            new FraudEvaluation
            {
                FraudEvaluationId = Guid.NewGuid(),
                TransactionId = allowedTransaction.TransactionId,
                RiskScore = 10,
                Decision = FraudDecision.Allow,
                EvaluatedAt = now
            });

        await dbContext.SaveChangesAsync();

        var workflow = new TransactionEvaluationWorkflowService(
            dbContext,
            new FakeFraudEvaluationService(
                new FraudEvaluationResultDto()));

        // Act
        var result = await workflow.GetFlaggedTransactionsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(
            newerTransaction.TransactionId,
            result[0].TransactionId);
        Assert.Equal(
            olderTransaction.TransactionId,
            result[1].TransactionId);

        Assert.All(
            result,
            transaction => Assert.Equal(
                FraudDecision.FlagForReview,
                transaction.Decision));
    }

    private static AppDbContext CreateDbContext()
    {
        var password = Environment.GetEnvironmentVariable(
            "TFD_TEST_DB_PASSWORD");

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Environment variable TFD_TEST_DB_PASSWORD is not set.");
        }

        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Database = "TFDDB_Test",
            Username = "tfduser",
            Password = password
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var dbContext = new AppDbContext(options);

        var databaseName = dbContext.Database
    .GetDbConnection()
    .Database;

        if (databaseName != "TFDDB_Test")
        {
            throw new InvalidOperationException(
                $"Tests must not run against database '{databaseName}'.");
        }

        dbContext.Database.EnsureCreated();
        dbContext.TriggeredRules.RemoveRange(dbContext.TriggeredRules);
        dbContext.FraudEvaluations.RemoveRange(dbContext.FraudEvaluations);
        dbContext.Transactions.RemoveRange(dbContext.Transactions);

        dbContext.SaveChanges();


        return dbContext;
    }

    private static CreateTransactionRequestDto CreateRequest()
    {
        return new CreateTransactionRequestDto
        {
            UserId = "user-1",
            Amount = 100,
            Currency = "SAR",
            MerchantId = "merchant-1"
        };
    }

    private sealed class FakeFraudEvaluationService
        : IFraudEvaluationService
    {
        private readonly FraudEvaluationResultDto _result;

        public FraudEvaluationContext? ReceivedContext { get; private set; }

        public FakeFraudEvaluationService(FraudEvaluationResultDto result)
        {
            _result = result;
        }

        public FraudEvaluationResultDto Evaluate(FraudEvaluationContext context)
        {
            ReceivedContext = context;
            return _result;
        }
    }
}
