
using Transaction_Fraud_Detection_API.FraudDetection;
using Transaction_Fraud_Detection_API.Models;
using Xunit;

namespace Transaction_Fraud_Detection_Test
{
    public class HighVelocityRuleTests
    {
        [Theory]
        [InlineData(4, false, 0)]
        [InlineData(5, true, 40)]
        public void Evaluate_PreviousTransactions_ReturnsExpectedResult(
            int previousTransactionCount,
            bool expectedIsTriggered,
            int expectedPoints)
        {
            // Arrange
            var rule = new HighVelocityRule();

            var currentTimestamp = new DateTimeOffset(
                2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

            var currentTransaction = new Transaction
            {
                UserId = "user-1",
                Amount = 100,
                Timestamp = currentTimestamp
            };

            var previousTransactions = Enumerable
                .Range(1, previousTransactionCount)
                .Select(i => new Transaction
                {
                    UserId = "user-1",
                    Amount = 100,
                    Timestamp = currentTimestamp.AddMinutes(-i)
                })
                .ToList();

            var context = new FraudEvaluationContext
            {
                CurrentTransaction = currentTransaction,
                PreviousTransactions = previousTransactions
            };

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.Equal(expectedIsTriggered, result.IsTriggered);
            Assert.Equal(expectedPoints, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_TransactionsFromOtherCustomers_DoesNotCountThem()
        {
            // Arrange
            var rule = new HighVelocityRule();
            var currentTimestamp = new DateTimeOffset(
                2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

            var currentTransaction = new Transaction
            {
                UserId = "user-1",
                Timestamp = currentTimestamp
            };

            var previousTransactions = Enumerable
                .Range(1, 6)
                .Select(i => new Transaction
                {
                    UserId = "user-2",
                    Timestamp = currentTimestamp.AddMinutes(-i)
                })
                .ToList();

            var context = new FraudEvaluationContext
            {
                CurrentTransaction = currentTransaction,
                PreviousTransactions = previousTransactions
            };

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_TransactionExactlyTenMinutesEarlier_CountsIt()
        {
            // Arrange
            var rule = new HighVelocityRule();
            var currentTimestamp = new DateTimeOffset(
                2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

            var currentTransaction = new Transaction
            {
                UserId = "user-1",
                Timestamp = currentTimestamp
            };

            var previousTransactions = Enumerable
                .Range(1, 4)
                .Select(i => new Transaction
                {
                    UserId = "user-1",
                    Timestamp = currentTimestamp.AddMinutes(-i)
                })
                .ToList();

            previousTransactions.Add(new Transaction
            {
                UserId = "user-1",
                Timestamp = currentTimestamp.AddMinutes(-10)
            });

            var context = new FraudEvaluationContext
            {
                CurrentTransaction = currentTransaction,
                PreviousTransactions = previousTransactions
            };

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.True(result.IsTriggered);
            Assert.Equal(40, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_TransactionOlderThanTenMinutes_DoesNotCountIt()
        {
            // Arrange
            var rule = new HighVelocityRule();
            var currentTimestamp = new DateTimeOffset(
                2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

            var currentTransaction = new Transaction
            {
                UserId = "user-1",
                Timestamp = currentTimestamp
            };

            var previousTransactions = Enumerable
                .Range(1, 4)
                .Select(i => new Transaction
                {
                    UserId = "user-1",
                    Timestamp = currentTimestamp.AddMinutes(-i)
                })
                .ToList();

            previousTransactions.Add(new Transaction
            {
                UserId = "user-1",
                Timestamp = currentTimestamp.AddMinutes(-10).AddTicks(-1)
            });

            var context = new FraudEvaluationContext
            {
                CurrentTransaction = currentTransaction,
                PreviousTransactions = previousTransactions
            };

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }
    }
}
