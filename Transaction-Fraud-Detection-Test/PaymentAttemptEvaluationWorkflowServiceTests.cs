using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
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

        private static PaymentAttemptRequestDto CreatePaymentAttemptRequest(
            string fingerprint = "test-payment-instrument")
        {
            return new PaymentAttemptRequestDto
            {
                PaymentInstrumentFingerprint = fingerprint
            };
        }

        private static string ComputeSha256Fingerprint(string value)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(value);
            byte[] hashBytes = SHA256.HashData(inputBytes);

            return Convert.ToHexString(hashBytes);
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

            var evaluation = new FraudEvaluation
            {
                TransactionId = transaction.TransactionId,
                RiskScore = 0,
                Decision = FraudDecision.Allow,
                EvaluatedAt = DateTimeOffset.UtcNow
            };

            dbContext.FraudEvaluations.Add(evaluation);
            await dbContext.SaveChangesAsync();

            return transaction;
        }

        // --------------------------------------------------
        // A. Payment Attempt Creation
        // --------------------------------------------------

        [Fact]
        public async Task RecordPaymentAttemptAsync_PreservesAmountAndCurrencySnapshots()
        {
            await using var dbContext = CreateDbContext();

            var transaction = await CreateTransactionAsync(
                dbContext,
                currency: "SAR",
                amount: 100);

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                new FakePaymentAttemptFraudEvaluationService());

            await workflow.RecordPaymentAttemptAsync(
                transaction.TransactionId,
                CreatePaymentAttemptRequest());

            // Change the transaction after the payment attempt was created.
            transaction.Amount = 200;
            transaction.Currency = "USD";

            await dbContext.SaveChangesAsync();

            // Read the payment attempt using a fresh DbContext.
            await using var verificationContext = CreateDbContext();

            var attempt = await verificationContext.PaymentAttempts
                .AsNoTracking()
                .SingleAsync(p => p.TransactionId == transaction.TransactionId);

            Assert.Equal(100m, attempt.Amount);
            Assert.Equal("SAR", attempt.Currency);
        }

        [Fact]
        public async Task RecordPaymentAttemptAsync_UnsupportedCurrency_SavesFailedAttemptAndEvaluation()
        {
            await using var dbContext = CreateDbContext();

            var transaction = await CreateTransactionAsync(
                dbContext,
                currency: "XYZ");

            var fraudService = new FakePaymentAttemptFraudEvaluationService();

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                fraudService);

            var result = await workflow.RecordPaymentAttemptAsync(
                transaction.TransactionId,
                CreatePaymentAttemptRequest());

            var attempt = await dbContext.PaymentAttempts
                .SingleAsync(p => p.TransactionId == transaction.TransactionId);

            Assert.Equal(PaymentAttemptStatus.Failed, attempt.Status);
            Assert.Equal("Unsupported currency", attempt.FailureReason);

            // Failed attempts are evaluated too.
            Assert.True(fraudService.WasCalled);
            Assert.Equal(1, fraudService.CallCount);

            var evaluation = await dbContext.PaymentAttemptFraudEvaluations
                .SingleAsync(e => e.PaymentAttemptId == attempt.PaymentAttemptId);

            Assert.Equal(0, evaluation.RiskScore);
            Assert.Equal(FraudDecision.Allow, evaluation.Decision);

            Assert.Equal(attempt.PaymentAttemptId, result.PaymentAttempt.PaymentAttemptId);
            Assert.Equal(PaymentAttemptStatus.Failed, result.PaymentAttempt.Status);
            Assert.Equal(0, result.PaymentAttempt.RiskScore);
            Assert.Empty(result.PaymentAttempt.TriggeredRules);

            Assert.Equal(0, result.TotalRiskScore);
            Assert.Empty(result.CombinedTriggeredRules);
            Assert.Equal(FraudDecision.Allow, result.FinalDecision);
        }

        [Fact]
        public async Task RecordPaymentAttemptAsync_FailedAttempt_PreservesHighTransactionRiskScore()
        {
            await using var dbContext = CreateDbContext();

            var transaction = await CreateTransactionAsync(
                dbContext,
                currency: "XYZ");

            var transactionEvaluation = await dbContext.FraudEvaluations
                .SingleAsync(e => e.TransactionId == transaction.TransactionId);

            transactionEvaluation.RiskScore = 60;
            transactionEvaluation.Decision = FraudDecision.FlagForReview;

            await dbContext.SaveChangesAsync();

            var fraudService = new FakePaymentAttemptFraudEvaluationService();

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                fraudService);

            var result = await workflow.RecordPaymentAttemptAsync(
                transaction.TransactionId,
                CreatePaymentAttemptRequest());

            Assert.Equal(PaymentAttemptStatus.Failed, result.PaymentAttempt.Status);

            // Transaction evaluation is preserved.
            Assert.Equal(60, result.Transaction.RiskScore);
            Assert.Equal(transaction.TransactionId, result.Transaction.TransactionId);

            // The failed attempt is also evaluated.
            Assert.True(fraudService.WasCalled);
            Assert.Equal(1, fraudService.CallCount);

            Assert.Equal(0, result.PaymentAttempt.RiskScore);
            Assert.Empty(result.PaymentAttempt.TriggeredRules);

            // Combined evaluation.
            Assert.Equal(60, result.TotalRiskScore);
            Assert.Empty(result.CombinedTriggeredRules);
            Assert.Equal(FraudDecision.FlagForReview, result.FinalDecision);
        }

        [Theory]
        [InlineData(49)]
        [InlineData(5501)]
        public async Task RecordPaymentAttemptAsync_AmountOutsideAllowedRange_SavesFailedAttemptAndEvaluation(
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

            await workflow.RecordPaymentAttemptAsync(
                transaction.TransactionId,
                CreatePaymentAttemptRequest());

            var attempt = await dbContext.PaymentAttempts
                .SingleAsync(p => p.TransactionId == transaction.TransactionId);

            Assert.Equal(PaymentAttemptStatus.Failed, attempt.Status);

            Assert.True(fraudService.WasCalled);
            Assert.Equal(1, fraudService.CallCount);

            Assert.True(await dbContext.PaymentAttemptFraudEvaluations
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

            await workflow.RecordPaymentAttemptAsync(
                transaction.TransactionId,
                CreatePaymentAttemptRequest());

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

            await workflow.RecordPaymentAttemptAsync(
                transaction.TransactionId,
                CreatePaymentAttemptRequest());

            var attempt = await dbContext.PaymentAttempts
                .SingleAsync(p => p.TransactionId == transaction.TransactionId);

            Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
        }

        [Fact]
        public async Task RecordPaymentAttemptAsync_HashesFingerprintBeforePersistence()
        {
            await using var dbContext = CreateDbContext();

            var transaction = await CreateTransactionAsync(dbContext);

            const string rawFingerprint = "test-payment-instrument";

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                new FakePaymentAttemptFraudEvaluationService());

            await workflow.RecordPaymentAttemptAsync(
                transaction.TransactionId,
                CreatePaymentAttemptRequest(rawFingerprint));

            var attempt = await dbContext.PaymentAttempts
                .AsNoTracking()
                .SingleAsync(p => p.TransactionId == transaction.TransactionId);

            var expectedHash = ComputeSha256Fingerprint(rawFingerprint);

            Assert.Equal(expectedHash, attempt.PaymentInstrumentFingerprint);
            Assert.NotEqual(rawFingerprint, attempt.PaymentInstrumentFingerprint);
            Assert.Equal(64, attempt.PaymentInstrumentFingerprint!.Length);
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

            await workflow.RecordPaymentAttemptAsync(
                transaction.TransactionId,
                CreatePaymentAttemptRequest());

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

            var transaction = await CreateTransactionAsync(dbContext, amount: 2000);

            var fakeEvaluationService = new FakePaymentAttemptFraudEvaluationService
            {
                Result = new FraudEvaluationResultDto
                {
                    RiskScore = 50,
                    Decision = FraudDecision.FlagForReview,
                    TriggeredRules = new List<TriggeredRuleResultDto>
                    {
                        new()
                        {
                            RuleCode = FraudRuleCode.PaymentFailuresFollowedBySuccess,
                            PointsAwarded = 20
                        },
                        new()
                        {
                            RuleCode = FraudRuleCode.HighAmount,
                            PointsAwarded = 30
                        }
                    }
                }
            };

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                fakeEvaluationService);

            var result = await workflow.RecordPaymentAttemptAsync(
                transaction.TransactionId,
                CreatePaymentAttemptRequest());

            var attempt = await dbContext.PaymentAttempts
                .SingleAsync(x => x.TransactionId == transaction.TransactionId);

            Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);

            var evaluation = await dbContext.PaymentAttemptFraudEvaluations
                .SingleAsync(x => x.PaymentAttemptId == attempt.PaymentAttemptId);

            Assert.Equal(50, evaluation.RiskScore);
            Assert.Equal(FraudDecision.FlagForReview, evaluation.Decision);

            var triggeredRules = await dbContext.PaymentAttemptTriggeredRules
                .Where(x =>
                    x.PaymentAttemptFraudEvaluationId ==
                    evaluation.PaymentAttemptFraudEvaluationId)
                .ToListAsync();

            Assert.Equal(2, triggeredRules.Count);

            Assert.Equal(transaction.TransactionId, result.Transaction.TransactionId);
            Assert.Equal(0, result.Transaction.RiskScore);
            Assert.Empty(result.Transaction.TriggeredRules);

            Assert.Equal(attempt.PaymentAttemptId, result.PaymentAttempt.PaymentAttemptId);
            Assert.Equal(PaymentAttemptStatus.Succeeded, result.PaymentAttempt.Status);
            Assert.Equal(50, result.PaymentAttempt.RiskScore);
            Assert.Equal(2, result.PaymentAttempt.TriggeredRules.Count);

            Assert.Equal(50, result.TotalRiskScore);
            Assert.Equal(2, result.CombinedTriggeredRules.Count);
            Assert.Equal(FraudDecision.FlagForReview, result.FinalDecision);

            Assert.All(
                result.CombinedTriggeredRules,
                rule => Assert.Equal(
                    FraudEvaluationSource.PaymentAttempt,
                    rule.Source));
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

            await workflow.RecordPaymentAttemptAsync(
                transaction.TransactionId,
                CreatePaymentAttemptRequest());

            Assert.Equal(1, fraudService.CallCount);
        }

        [Fact]
        public async Task RecordPaymentAttemptAsync_FailedAttempt_CallsEvaluationServiceExactlyOnce()
        {
            await using var dbContext = CreateDbContext();

            var transaction = await CreateTransactionAsync(
                dbContext,
                currency: "XYZ");

            var fraudService = new FakePaymentAttemptFraudEvaluationService();

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                fraudService);

            await workflow.RecordPaymentAttemptAsync(
                transaction.TransactionId,
                CreatePaymentAttemptRequest());

            Assert.Equal(1, fraudService.CallCount);
        }

        // --------------------------------------------------
        // C. Combined Fraud Evaluation
        // --------------------------------------------------

        [Fact]
        public async Task RecordPaymentAttemptAsync_CombinesTransactionAndPaymentAttemptRiskScores()
        {
            await using var dbContext = CreateDbContext();

            var transaction = await CreateTransactionAsync(dbContext);

            var transactionEvaluation = await dbContext.FraudEvaluations
                .SingleAsync(e => e.TransactionId == transaction.TransactionId);

            transactionEvaluation.RiskScore = 30;
            transactionEvaluation.Decision = FraudDecision.Allow;

            dbContext.TriggeredRules.Add(new TriggeredRule
            {
                TriggeredRuleId = Guid.NewGuid(),
                FraudEvaluationId = transactionEvaluation.FraudEvaluationId,
                RuleCode = FraudRuleCode.HighAmount,
                PointsAwarded = 30
            });

            await dbContext.SaveChangesAsync();

            var fraudService = new FakePaymentAttemptFraudEvaluationService
            {
                Result = new FraudEvaluationResultDto
                {
                    RiskScore = 40,
                    Decision = FraudDecision.Allow,
                    TriggeredRules = new List<TriggeredRuleResultDto>
                    {
                        new()
                        {
                            RuleCode = FraudRuleCode.PaymentFailuresFollowedBySuccess,
                            PointsAwarded = 40
                        }
                    }
                }
            };

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                fraudService);

            var result = await workflow.RecordPaymentAttemptAsync(
                transaction.TransactionId,
                CreatePaymentAttemptRequest());

            Assert.Equal(30, result.Transaction.RiskScore);
            Assert.Single(result.Transaction.TriggeredRules);

            Assert.Equal(40, result.PaymentAttempt.RiskScore);
            Assert.Single(result.PaymentAttempt.TriggeredRules);

            Assert.Equal(70, result.TotalRiskScore);
            Assert.Equal(FraudDecision.FlagForReview, result.FinalDecision);

            Assert.Equal(2, result.CombinedTriggeredRules.Count);

            Assert.Contains(
                result.CombinedTriggeredRules,
                rule => rule.RuleCode == FraudRuleCode.HighAmount
                        && rule.PointsAwarded == 30
                        && rule.Source == FraudEvaluationSource.Transaction);

            Assert.Contains(
                result.CombinedTriggeredRules,
                rule => rule.RuleCode == FraudRuleCode.PaymentFailuresFollowedBySuccess
                        && rule.PointsAwarded == 40
                        && rule.Source == FraudEvaluationSource.PaymentAttempt);
        }

        // --------------------------------------------------
        // D. Previous Failed Attempts
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
                Currency = previousTransaction.Currency,
                Amount = previousTransaction.Amount,
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

            await workflow.RecordPaymentAttemptAsync(
                transaction.TransactionId,
                CreatePaymentAttemptRequest());

            Assert.True(fraudService.WasCalled);

            Assert.Contains(
                fraudService.ReceivedContext!.PreviousFailedAttempts,
                p => p.PaymentAttemptId == previousAttempt.PaymentAttemptId);
        }

        [Fact]
        public async Task RecordPaymentAttemptAsync_PreviousFailedAttemptsFromAnotherUser_AreExcluded()
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
                Currency = anotherTransaction.Currency,
                Amount = anotherTransaction.Amount,
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

            await workflow.RecordPaymentAttemptAsync(
                transaction.TransactionId,
                CreatePaymentAttemptRequest());

            Assert.DoesNotContain(
                fraudService.ReceivedContext!.PreviousFailedAttempts,
                p => p.PaymentAttemptId == previousAttempt.PaymentAttemptId);
        }

        // --------------------------------------------------
        // E. Shared Payment Instrument History
        // --------------------------------------------------

        [Fact]
        public async Task RecordPaymentAttemptAsync_PreviousSuccessfulAttemptWithSameFingerprint_LoadsPreviousUserId()
        {
            await using var dbContext = CreateDbContext();

            const string rawFingerprint = "shared-test-payment-instrument";
            string fingerprintHash = ComputeSha256Fingerprint(rawFingerprint);

            var currentUserId = Guid.NewGuid().ToString();
            var previousUserId = Guid.NewGuid().ToString();

            var currentTransaction = await CreateTransactionAsync(
                dbContext,
                userId: currentUserId);

            var previousTransaction = await CreateTransactionAsync(
                dbContext,
                userId: previousUserId);

            var previousAttempt = new PaymentAttempt
            {
                PaymentAttemptId = Guid.NewGuid(),
                TransactionId = previousTransaction.TransactionId,
                Currency = previousTransaction.Currency,
                Amount = previousTransaction.Amount,
                Status = PaymentAttemptStatus.Succeeded,
                PaymentInstrumentFingerprint = fingerprintHash,
                AttemptedAt = DateTimeOffset.UtcNow.AddHours(-1)
            };

            dbContext.PaymentAttempts.Add(previousAttempt);
            await dbContext.SaveChangesAsync();

            var fraudService = new FakePaymentAttemptFraudEvaluationService();

            var workflow = new PaymentAttemptEvaluationWorkflowService(
                dbContext,
                fraudService);

            await workflow.RecordPaymentAttemptAsync(
                currentTransaction.TransactionId,
                CreatePaymentAttemptRequest(rawFingerprint));

            Assert.Contains(
                previousUserId,
                fraudService.ReceivedContext!.PreviousUserIdsWithSameInstrument);
        }

        // --------------------------------------------------
        // F. Error Handling
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
                () => workflow.RecordPaymentAttemptAsync(
                    Guid.NewGuid(),
                    CreatePaymentAttemptRequest()));

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