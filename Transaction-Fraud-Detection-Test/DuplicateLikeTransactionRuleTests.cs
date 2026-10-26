
using Transaction_Fraud_Detection_API.FraudDetection;
using Transaction_Fraud_Detection_API.Models;
using Xunit;

namespace Transaction_Fraud_Detection_Test
{
    public class DuplicateLikeTransactionRuleTests
    {
        private static readonly DateTimeOffset CurrentTimestamp =
            new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

        private static Transaction CreateTransaction(
            string userId = "user-1",
            string merchantId = "merchant-1",
            decimal amount = 100,
            string currency = "SAR",
            DateTimeOffset? timestamp = null)
        {
            return new Transaction
            {
                UserId = userId,
                MerchantId = merchantId,
                Amount = amount,
                Currency = currency,
                Timestamp = timestamp ?? CurrentTimestamp
            };
        }

        private static FraudEvaluationContext CreateContext(params Transaction[] previousTransactions)
        {
            return new FraudEvaluationContext
            {
                CurrentTransaction = CreateTransaction(),
                PreviousTransactions = previousTransactions
            };
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(true, 50)]
        public void Evaluate_MatchingPreviousTransaction_ReturnsExpectedResult(
            bool hasMatchingTransaction,
            int expectedPoints)
        {
            // Arrange
            var rule = new DuplicateLikeTransactionRule();

            var previousTransactions = hasMatchingTransaction
                ? new[] { CreateTransaction(timestamp: CurrentTimestamp.AddMinutes(-1)) }
                : Array.Empty<Transaction>();

            var context = CreateContext(previousTransactions);

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.Equal(hasMatchingTransaction, result.IsTriggered);
            Assert.Equal(expectedPoints, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_DifferentCustomer_DoesNotTrigger()
        {
            // Arrange
            var rule = new DuplicateLikeTransactionRule();
            var previous = CreateTransaction(
                userId: "user-2",
                timestamp: CurrentTimestamp.AddMinutes(-1));

            // Act
            var result = rule.Evaluate(CreateContext(previous));

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_DifferentMerchant_DoesNotTrigger()
        {
            // Arrange
            var rule = new DuplicateLikeTransactionRule();
            var previous = CreateTransaction(
                merchantId: "merchant-2",
                timestamp: CurrentTimestamp.AddMinutes(-1));

            // Act
            var result = rule.Evaluate(CreateContext(previous));

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Theory]
        [InlineData(200, "SAR")]
        [InlineData(100, "USD")]
        public void Evaluate_DifferentAmountOrCurrency_DoesNotTrigger(
            decimal amount,
            string currency)
        {
            // Arrange
            var rule = new DuplicateLikeTransactionRule();
            var previous = CreateTransaction(
                amount: amount,
                currency: currency,
                timestamp: CurrentTimestamp.AddMinutes(-1));

            // Act
            var result = rule.Evaluate(CreateContext(previous));

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_TransactionExactlyTwoMinutesEarlier_Triggers()
        {
            // Arrange
            var rule = new DuplicateLikeTransactionRule();
            var previous = CreateTransaction(
                timestamp: CurrentTimestamp.AddMinutes(-2));

            // Act
            var result = rule.Evaluate(CreateContext(previous));

            // Assert
            Assert.True(result.IsTriggered);
            Assert.Equal(50, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_TransactionOlderThanTwoMinutes_DoesNotTrigger()
        {
            // Arrange
            var rule = new DuplicateLikeTransactionRule();
            var previous = CreateTransaction(
                timestamp: CurrentTimestamp.AddMinutes(-2).AddTicks(-1));

            // Act
            var result = rule.Evaluate(CreateContext(previous));

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_TransactionAfterCurrentTimestamp_DoesNotTrigger()
        {
            // Arrange
            var rule = new DuplicateLikeTransactionRule();
            var previous = CreateTransaction(
                timestamp: CurrentTimestamp.AddSeconds(1));

            // Act
            var result = rule.Evaluate(CreateContext(previous));

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }
    }
}
