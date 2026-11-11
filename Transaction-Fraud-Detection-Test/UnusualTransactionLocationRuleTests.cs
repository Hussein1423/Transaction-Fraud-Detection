using System;
using Transaction_Fraud_Detection_API.FraudDetection;
using Transaction_Fraud_Detection_API.Models;
using Xunit;

namespace Transaction_Fraud_Detection_Test
{
    public class UnusualTransactionLocationRuleTests
    {
        private static readonly DateTimeOffset CurrentTimestamp =
            new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

        private static Transaction CreateTransaction(
            string userId = "user-1",
            string? city = "Riyadh",
            string? country = "Saudi Arabia",
            DateTimeOffset? timestamp = null)
        {
            return new Transaction
            {
                UserId = userId,
                MerchantId = "merchant-1",
                Amount = 100,
                Currency = "SAR",
                Timestamp = timestamp ?? CurrentTimestamp,
                City = city,
                Country = country
            };
        }

        private static FraudEvaluationContext CreateContext(
            Transaction? currentTransaction = null,
            params Transaction[] previousTransactions)
        {
            return new FraudEvaluationContext
            {
                CurrentTransaction = currentTransaction ?? CreateTransaction(),
                PreviousTransactions = previousTransactions
            };
        }

        [Fact]
        public void Evaluate_NoPreviousTransactions_DoesNotTrigger()
        {
            // Arrange
            var rule = new UnusualTransactionLocationRule();
            var context = CreateContext();

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_SameCityAndCountry_DoesNotTrigger()
        {
            // Arrange
            var rule = new UnusualTransactionLocationRule();

            var previous = CreateTransaction(
                timestamp: CurrentTimestamp.AddMinutes(-30));

            var context = CreateContext(
                previousTransactions: new[] { previous });

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_DifferentCity_TriggersWith15Points()
        {
            // Arrange
            var rule = new UnusualTransactionLocationRule();

            var previous = CreateTransaction(
                city: "Jeddah",
                timestamp: CurrentTimestamp.AddMinutes(-30));

            var context = CreateContext(
                previousTransactions: new[] { previous });

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.True(result.IsTriggered);
            Assert.Equal(15, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_DifferentCountry_TriggersWith15Points()
        {
            // Arrange
            var rule = new UnusualTransactionLocationRule();

            var previous = CreateTransaction(
                city: "Riyadh",
                country: "United States",
                timestamp: CurrentTimestamp.AddMinutes(-30));

            var context = CreateContext(
                previousTransactions: new[] { previous });

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.True(result.IsTriggered);
            Assert.Equal(15, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_MultiplePreviousTransactionsWithOneDifferentLocation_TriggersOnce()
        {
            // Arrange
            var rule = new UnusualTransactionLocationRule();

            var previousTransactions = new[]
            {
                CreateTransaction(
                    city: "Riyadh",
                    timestamp: CurrentTimestamp.AddMinutes(-10)),

                CreateTransaction(
                    city: "Riyadh",
                    timestamp: CurrentTimestamp.AddMinutes(-20)),

                CreateTransaction(
                    city: "Jeddah",
                    timestamp: CurrentTimestamp.AddMinutes(-30))
            };

            var context = CreateContext(
                previousTransactions: previousTransactions);

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.True(result.IsTriggered);
            Assert.Equal(15, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_CurrentLocationMissing_DoesNotTrigger()
        {
            // Arrange
            var rule = new UnusualTransactionLocationRule();

            var currentTransaction = CreateTransaction(
                city: null,
                country: null);

            var previous = CreateTransaction(
                city: "Jeddah",
                country: "Saudi Arabia",
                timestamp: CurrentTimestamp.AddMinutes(-30));

            var context = CreateContext(
                currentTransaction,
                previous);

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_HistoricalLocationMissing_DoesNotTrigger()
        {
            // Arrange
            var rule = new UnusualTransactionLocationRule();

            var previous = CreateTransaction(
                city: null,
                country: null,
                timestamp: CurrentTimestamp.AddMinutes(-30));

            var context = CreateContext(
                previousTransactions: new[] { previous });

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_HistoricalCityMissing_CurrentCityAvailable_Triggers()
        {
            // Arrange
            var rule = new UnusualTransactionLocationRule();

            var previous = CreateTransaction(
                city: null,
                country: "Saudi Arabia",
                timestamp: CurrentTimestamp.AddMinutes(-30));

            var context = CreateContext(
                previousTransactions: new[] { previous });

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.True(result.IsTriggered);
            Assert.Equal(15, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_HistoricalCountryMissing_CurrentCountryAvailable_Triggers()
        {
            // Arrange
            var rule = new UnusualTransactionLocationRule();

            var previous = CreateTransaction(
                city: "Riyadh",
                country: null,
                timestamp: CurrentTimestamp.AddMinutes(-30));

            var context = CreateContext(
                previousTransactions: new[] { previous });

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.True(result.IsTriggered);
            Assert.Equal(15, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_CityAndCountryDifferOnlyByCaseOrWhitespace_DoesNotTrigger()
        {
            // Arrange
            var rule = new UnusualTransactionLocationRule();

            var previous = CreateTransaction(
                city: " riyadh ",
                country: "saudi arabia",
                timestamp: CurrentTimestamp.AddMinutes(-30));

            var context = CreateContext(
                previousTransactions: new[] { previous });

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_PreviousTransactionExactlyTwoHoursEarlier_Triggers()
        {
            // Arrange
            var rule = new UnusualTransactionLocationRule();

            var previous = CreateTransaction(
                city: "Jeddah",
                timestamp: CurrentTimestamp.AddHours(-2));

            var context = CreateContext(
                previousTransactions: new[] { previous });

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.True(result.IsTriggered);
            Assert.Equal(15, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_PreviousTransactionOlderThanTwoHours_DoesNotTrigger()
        {
            // Arrange
            var rule = new UnusualTransactionLocationRule();

            var previous = CreateTransaction(
                city: "Jeddah",
                timestamp: CurrentTimestamp.AddHours(-2).AddTicks(-1));

            var context = CreateContext(
                previousTransactions: new[] { previous });

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }
    }
}