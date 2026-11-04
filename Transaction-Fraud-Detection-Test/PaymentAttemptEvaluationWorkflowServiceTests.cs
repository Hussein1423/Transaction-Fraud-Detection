using Microsoft.EntityFrameworkCore;
using Transaction_Fraud_Detection_API.Data;
using Transaction_Fraud_Detection_API.DTOs;
using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection;
using Transaction_Fraud_Detection_API.FraudDetection.Interfaces;
using Transaction_Fraud_Detection_API.FraudDetection.Services;
using Transaction_Fraud_Detection_API.Models;
using Xunit;

namespace Transaction_Fraud_Detection_Test
{
    [Collection("SharedDatabase")]
    public class PaymentAttemptEvaluationWorkflowServiceTests
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public PaymentAttemptEvaluationWorkflowServiceTests()
        {
            var password = Environment.GetEnvironmentVariable("TFD_TEST_DB_PASSWORD");

            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException(
                    "TFD_TEST_DB_PASSWORD environment variable is missing.");

            var connectionString =
                $"Host=localhost;Port=5432;Database=TFDDB_Test;Username=tfduser;Password={password}";

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            using var context = new AppDbContext(_options);

            context.Database.Migrate();
        }

        private AppDbContext CreateDbContext()
        {
            return new AppDbContext(_options);
        }

        private async Task<Transaction> CreateTransactionAsync(
            AppDbContext dbContext,
            string currency = "SAR",
            decimal amount = 100,
            string? userId = null)
        {
            var transaction = new Transaction
            {
                TransactionId = Guid.NewGuid(),
                UserId = userId ?? Guid.NewGuid().ToString(),
                Amount = amount,
                Currency = currency,
                MerchantId = "merchant-1",
                Timestamp = DateTimeOffset.UtcNow
            };

            dbContext.Transactions.Add(transaction);
            await dbContext.SaveChangesAsync();

            return transaction;
        }

        // --------------------------------------------------
        // A. Payment Attempt Creation
        // --------------------------------------------------

        [Fact]
        public async Task RecordPaymentAttemptAsync_UnsupportedCurrency_SavesFailedAttemptWithoutEvaluation()
        {
            await using var dbContext = CreateDbContext();

            var transaction = await CreateTransactionAsync(
                dbContext,
                currency: "XYZ");

            var fraudService = new FakePaymentAttemptFraudEvaluationService();

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                fraudService);

            await workflow.RecordPaymentAttemptAsync(transaction.TransactionId);

            var attempt = await dbContext.PaymentAttempts
                .SingleAsync(p => p.TransactionId == transaction.TransactionId);

            Assert.Equal(PaymentAttemptStatus.Failed, attempt.Status);
            Assert.Equal("Unsupported currency", attempt.FailureReason);

            Assert.False(fraudService.WasCalled);


            var evaluation = await dbContext.PaymentAttemptFraudEvaluations
            .SingleOrDefaultAsync(e => e.PaymentAttemptId == attempt.PaymentAttemptId);

            Assert.Null(evaluation);
        }

        [Theory]
        [InlineData(49)]
        [InlineData(5501)]
        public async Task RecordPaymentAttemptAsync_AmountOutsideAllowedRange_SavesFailedAttempt(
            decimal amount)
        {
            await using var dbContext = CreateDbContext();

            var transaction = await CreateTransactionAsync(
                dbContext,
                amount: amount);

            var fraudService = new FakePaymentAttemptFraudEvaluationService();

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                fraudService);

            await workflow.RecordPaymentAttemptAsync(transaction.TransactionId);

            var attempt = await dbContext.PaymentAttempts
                .SingleAsync(p => p.TransactionId == transaction.TransactionId);

            Assert.Equal(PaymentAttemptStatus.Failed, attempt.Status);
            Assert.False(fraudService.WasCalled);

            Assert.False(await dbContext.PaymentAttemptFraudEvaluations
                .AnyAsync(e => e.PaymentAttemptId == attempt.PaymentAttemptId));
        }

        [Theory]
        [InlineData(50)]
        [InlineData(5500)]
        public async Task RecordPaymentAttemptAsync_AmountAtAllowedBoundary_SavesSuccessfulAttempt(
            decimal amount)
        {
            await using var dbContext = CreateDbContext();

            var transaction = await CreateTransactionAsync(
                dbContext,
                amount: amount);

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                new FakePaymentAttemptFraudEvaluationService());

            await workflow.RecordPaymentAttemptAsync(transaction.TransactionId);

            var attempt = await dbContext.PaymentAttempts
                .SingleAsync(p => p.TransactionId == transaction.TransactionId);

            Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
        }

        [Theory]
        [InlineData("SAR")]
        [InlineData("USD")]
        [InlineData("YER")]
        [InlineData("JOD")]
        [InlineData("sar")]
        [InlineData("usd")]
        public async Task RecordPaymentAttemptAsync_SupportedCurrency_SavesSuccessfulAttempt(
            string currency)
        {
            await using var dbContext = CreateDbContext();

            var transaction = await CreateTransactionAsync(
                dbContext,
                currency: currency);

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                new FakePaymentAttemptFraudEvaluationService());

            await workflow.RecordPaymentAttemptAsync(transaction.TransactionId);

            var attempt = await dbContext.PaymentAttempts
                .SingleAsync(p => p.TransactionId == transaction.TransactionId);

            Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
        }

        // --------------------------------------------------
        // B. Fraud Evaluation
        // --------------------------------------------------

        [Fact]
        public async Task RecordPaymentAttemptAsync_SuccessfulAttemptWithoutTriggeredRules_SavesEvaluation()
        {
            await using var dbContext = CreateDbContext();

            var transaction = await CreateTransactionAsync(dbContext);

            var fraudService = new FakePaymentAttemptFraudEvaluationService
            {
                Result = new FraudEvaluationResultDto
                {
                    RiskScore = 0,
                    Decision = FraudDecision.Allow
                }
            };

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                fraudService);

            await workflow.RecordPaymentAttemptAsync(transaction.TransactionId);

            var attempt = await dbContext.PaymentAttempts
                .SingleAsync(p => p.TransactionId == transaction.TransactionId);

            var evaluation = await dbContext.PaymentAttemptFraudEvaluations
                .SingleAsync(e => e.PaymentAttemptId == attempt.PaymentAttemptId);

            Assert.True(fraudService.WasCalled);

            Assert.Equal(0, evaluation.RiskScore);
            Assert.Equal(FraudDecision.Allow, evaluation.Decision);

            Assert.False(await dbContext.PaymentAttemptTriggeredRules
                .AnyAsync(r =>
                    r.PaymentAttemptFraudEvaluationId ==
                    evaluation.PaymentAttemptFraudEvaluationId));
        }

        [Fact]
        public async Task RecordPaymentAttemptAsync_SuccessfulAttemptWithTriggeredRules_SavesEvaluationAndRules()
        {
            await using var dbContext = CreateDbContext();

            var transaction = await CreateTransactionAsync(dbContext);

            var fraudService = new FakePaymentAttemptFraudEvaluationService
            {
                Result = new FraudEvaluationResultDto
                {
                    RiskScore = 50,
                    Decision = FraudDecision.FlagForReview,
                    TriggeredRules =
                    {
                        new TriggeredRuleResultDto
                        {
                            RuleCode = FraudRuleCode.PaymentFailuresFollowedBySuccess,
                            PointsAwarded = 20
                        },
                        new TriggeredRuleResultDto
                        {
                            RuleCode = FraudRuleCode.HighAmount,
                            PointsAwarded = 30
                        }
                    }
                }
            };

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                fraudService);

            await workflow.RecordPaymentAttemptAsync(transaction.TransactionId);

            var attempt = await dbContext.PaymentAttempts
                .SingleAsync(p => p.TransactionId == transaction.TransactionId);

            var evaluation = await dbContext.PaymentAttemptFraudEvaluations
                .SingleAsync(e => e.PaymentAttemptId == attempt.PaymentAttemptId);

            var triggeredRules = await dbContext.PaymentAttemptTriggeredRules
                .Where(r =>
                    r.PaymentAttemptFraudEvaluationId ==
                    evaluation.PaymentAttemptFraudEvaluationId)
                .ToListAsync();

            Assert.True(fraudService.WasCalled);

            Assert.Equal(50, evaluation.RiskScore);
            Assert.Equal(FraudDecision.FlagForReview, evaluation.Decision);

            Assert.Equal(2, triggeredRules.Count);
            Assert.Contains(triggeredRules, r =>
                r.RuleCode == FraudRuleCode.PaymentFailuresFollowedBySuccess
                && r.PointsAwarded == 20);
        }

        [Fact]
        public async Task RecordPaymentAttemptAsync_SuccessfulAttempt_CallsEvaluationServiceExactlyOnce()
        {
            await using var dbContext = CreateDbContext();

            var transaction = await CreateTransactionAsync(dbContext);

            var fraudService = new FakePaymentAttemptFraudEvaluationService();

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                fraudService);

            await workflow.RecordPaymentAttemptAsync(transaction.TransactionId);

            Assert.Equal(1, fraudService.CallCount);
        }

        [Fact]
        public async Task RecordPaymentAttemptAsync_FailedAttempt_DoesNotCallEvaluationService()
        {
            await using var dbContext = CreateDbContext();

            var transaction = await CreateTransactionAsync(
                dbContext,
                currency: "XYZ");

            var fraudService = new FakePaymentAttemptFraudEvaluationService();

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                fraudService);

            await workflow.RecordPaymentAttemptAsync(transaction.TransactionId);

            Assert.Equal(0, fraudService.CallCount);
        }

        // --------------------------------------------------
        // C. Previous Payment Attempts
        // --------------------------------------------------

        [Fact]
        public async Task RecordPaymentAttemptAsync_PreviousFailedAttempts_PassesSameUserAttemptsToEvaluationService()
        {
            await using var dbContext = CreateDbContext();

            var userId = Guid.NewGuid().ToString();

            var transaction = await CreateTransactionAsync(
                dbContext,
                userId: userId);

            var previousTransaction = await CreateTransactionAsync(
                dbContext,
                currency: "XYZ",
                userId: userId);

            var previousAttempt = new PaymentAttempt
            {
                PaymentAttemptId = Guid.NewGuid(),
                TransactionId = previousTransaction.TransactionId,
                Status = PaymentAttemptStatus.Failed,
                FailureReason = "Unsupported currency",
                AttemptedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            };

            dbContext.PaymentAttempts.Add(previousAttempt);
            await dbContext.SaveChangesAsync();

            var fraudService = new FakePaymentAttemptFraudEvaluationService();

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                fraudService);

            await workflow.RecordPaymentAttemptAsync(transaction.TransactionId);

            Assert.True(fraudService.WasCalled);

            Assert.Contains(
                fraudService.ReceivedContext!.PreviousPaymentAttempts,
                p => p.PaymentAttemptId == previousAttempt.PaymentAttemptId);
        }

        [Fact]
        public async Task RecordPaymentAttemptAsync_PreviousAttemptsFromAnotherUser_AreExcluded()
        {
            await using var dbContext = CreateDbContext();

            var transaction = await CreateTransactionAsync(dbContext);

            var anotherTransaction = await CreateTransactionAsync(
                dbContext,
                currency: "XYZ");

            var previousAttempt = new PaymentAttempt
            {
                PaymentAttemptId = Guid.NewGuid(),
                TransactionId = anotherTransaction.TransactionId,
                Status = PaymentAttemptStatus.Failed,
                FailureReason = "Unsupported currency",
                AttemptedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            };

            dbContext.PaymentAttempts.Add(previousAttempt);
            await dbContext.SaveChangesAsync();

            var fraudService = new FakePaymentAttemptFraudEvaluationService();

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                fraudService);

            await workflow.RecordPaymentAttemptAsync(transaction.TransactionId);

            Assert.DoesNotContain(
                fraudService.ReceivedContext!.PreviousPaymentAttempts,
                p => p.PaymentAttemptId == previousAttempt.PaymentAttemptId);
        }

        // --------------------------------------------------
        // D. Error Handling
        // --------------------------------------------------

        [Fact]
        public async Task RecordPaymentAttemptAsync_NonExistingTransaction_ThrowsKeyNotFoundException()
        {
            await using var dbContext = CreateDbContext();

            var fraudService = new FakePaymentAttemptFraudEvaluationService();

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                fraudService);

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => workflow.RecordPaymentAttemptAsync(Guid.NewGuid()));

            Assert.False(fraudService.WasCalled);
        }

        // --------------------------------------------------
        // Fake Evaluation Service
        // --------------------------------------------------

        private sealed class FakePaymentAttemptFraudEvaluationService
            : IPaymentAttemptFraudEvaluationService
        {
            public bool WasCalled { get; private set; }

            public int CallCount { get; private set; }

            public PaymentAttemptEvaluationContext? ReceivedContext { get; private set; }

            public FraudEvaluationResultDto Result { get; set; } =
                new()
                {
                    RiskScore = 0,
                    Decision = FraudDecision.Allow
                };

            public FraudEvaluationResultDto Evaluate(
                PaymentAttemptEvaluationContext context)
            {
                WasCalled = true;
                CallCount++;

                ReceivedContext = context;

                return Result;
            }
        }
    }
}