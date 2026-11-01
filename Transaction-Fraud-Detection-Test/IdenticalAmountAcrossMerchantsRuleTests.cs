
using Transaction_Fraud_Detection_API.FraudDetection;
using Transaction_Fraud_Detection_API.Models;
using Xunit;

namespace Transaction_Fraud_Detection_Test
{
    public class IdenticalAmountAcrossMerchantsRuleTests
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

        private static FraudEvaluationContext CreateContext(Transaction? currentTransaction = null,params Transaction[] previousTransactions)
        {
            return new FraudEvaluationContext
            {
                CurrentTransaction = currentTransaction ?? CreateTransaction(),
                PreviousTransactions = previousTransactions
            };
        }



        [Fact]
        public void Evaluate_FourDistinctMerchantsWithWholeAmounts_TriggersWith10Points()
        {
            // Arrange
            var rule = new IdenticalAmountAcrossMerchantsRule();

            var previousTransactions = new[]
            {
                CreateTransaction(merchantId: "merchant-2", amount: 101),
                CreateTransaction(merchantId: "merchant-3", amount: 102),
                CreateTransaction(merchantId: "merchant-4", amount: 103)
            };

            var context = CreateContext(previousTransactions);

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.True(result.IsTriggered);
            Assert.Equal(10, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_FourDistinctMerchantsWithNonRoundAmounts_TriggersWith20Points()
        {
            // Arrange
            var rule = new IdenticalAmountAcrossMerchantsRule();

            var previousTransactions = new[]
            {
        CreateTransaction(merchantId: "merchant-2", amount: 37.42m),
        CreateTransaction(merchantId: "merchant-3", amount: 38.00m),
        CreateTransaction(merchantId: "merchant-4", amount: 39.50m)
    };

            var currentTransaction = CreateTransaction(
                amount: 40.10m);

            var context = CreateContext(
                currentTransaction,
                previousTransactions);

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.True(result.IsTriggered);
            Assert.Equal(20, result.PointsAwarded);
        }
        [Fact]
        public void Evaluate_OnlyThreeDistinctMerchants_DoesNotTrigger()
        {
            // Arrange
            var rule = new IdenticalAmountAcrossMerchantsRule();

            var previousTransactions = new[]
            {
                CreateTransaction(merchantId: "merchant-2", amount: 101),
                CreateTransaction(merchantId: "merchant-3", amount: 102)
            };

            var context = CreateContext(previousTransactions);

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_MultipleTransactionsFromSameMerchant_CountsMerchantOnce()
        {
            // Arrange
            var rule = new IdenticalAmountAcrossMerchantsRule();

            var previousTransactions = new[]
            {
                CreateTransaction(merchantId: "merchant-2", amount: 100),
                CreateTransaction(merchantId: "merchant-2", amount: 101),
                CreateTransaction(merchantId: "merchant-3", amount: 102)
            };

            var context = CreateContext(previousTransactions);

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_DifferentUser_DoesNotTrigger()
        {
            // Arrange
            var rule = new IdenticalAmountAcrossMerchantsRule();

            var previousTransactions = new[]
            {
                CreateTransaction(userId: "user-2", merchantId: "merchant-2"),
                CreateTransaction(userId: "user-2", merchantId: "merchant-3"),
                CreateTransaction(userId: "user-2", merchantId: "merchant-4")
            };

            var context = CreateContext(previousTransactions);

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_DifferentCurrency_DoesNotTrigger()
        {
            // Arrange
            var rule = new IdenticalAmountAcrossMerchantsRule();

            var previousTransactions = new[]
            {
                CreateTransaction(merchantId: "merchant-2", currency: "USD"),
                CreateTransaction(merchantId: "merchant-3", currency: "USD"),
                CreateTransaction(merchantId: "merchant-4", currency: "USD")
            };

            var context = CreateContext(previousTransactions);

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_AmountRangeExactlyThree_Triggers()
        {
            // Arrange
            var rule = new IdenticalAmountAcrossMerchantsRule();

            var previousTransactions = new[]
            {
                CreateTransaction(merchantId: "merchant-2", amount: 101),
                CreateTransaction(merchantId: "merchant-3", amount: 102),
                CreateTransaction(merchantId: "merchant-4", amount: 103)
            };

            var context = CreateContext(previousTransactions);

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.True(result.IsTriggered);
            Assert.Equal(10, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_AmountRangeGreaterThanThree_DoesNotTrigger()
        {
            // Arrange
            var rule = new IdenticalAmountAcrossMerchantsRule();

            var previousTransactions = new[]
            {
                CreateTransaction(merchantId: "merchant-2", amount: 101),
                CreateTransaction(merchantId: "merchant-3", amount: 102),
                CreateTransaction(merchantId: "merchant-4", amount: 104)
            };

            var context = CreateContext(previousTransactions);

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_TransactionExactlyFiveMinutesEarlier_IsIncluded()
        {
            // Arrange
            var rule = new IdenticalAmountAcrossMerchantsRule();

            var previousTransactions = new[]
            {
                CreateTransaction(
                    merchantId: "merchant-2",
                    timestamp: CurrentTimestamp.AddMinutes(-5)),
                CreateTransaction(
                    merchantId: "merchant-3",
                    timestamp: CurrentTimestamp.AddMinutes(-4)),
                CreateTransaction(
                    merchantId: "merchant-4",
                    timestamp: CurrentTimestamp.AddMinutes(-3))
            };

            var context = CreateContext(previousTransactions);

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.True(result.IsTriggered);
            Assert.Equal(10, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_TransactionOlderThanFiveMinutes_IsExcluded()
        {
            // Arrange
            var rule = new IdenticalAmountAcrossMerchantsRule();

            var previousTransactions = new[]
            {
                CreateTransaction(
                    merchantId: "merchant-2",
                    timestamp: CurrentTimestamp.AddMinutes(-5).AddTicks(-1)),
                CreateTransaction(
                    merchantId: "merchant-3",
                    timestamp: CurrentTimestamp.AddMinutes(-4)),
                CreateTransaction(
                    merchantId: "merchant-4",
                    timestamp: CurrentTimestamp.AddMinutes(-3))
            };

            var context = CreateContext(previousTransactions);

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_FutureTransaction_IsExcluded()
        {
            // Arrange
            var rule = new IdenticalAmountAcrossMerchantsRule();

            var previousTransactions = new[]
            {
                CreateTransaction(
                    merchantId: "merchant-2",
                    timestamp: CurrentTimestamp.AddSeconds(1)),
                CreateTransaction(
                    merchantId: "merchant-3",
                    timestamp: CurrentTimestamp.AddMinutes(-2)),
                CreateTransaction(
                    merchantId: "merchant-4",
                    timestamp: CurrentTimestamp.AddMinutes(-1))
            };

            var context = CreateContext(previousTransactions);

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_CurrentTransactionCompletesFourthMerchant_Triggers()
        {
            // Arrange
            var rule = new IdenticalAmountAcrossMerchantsRule();

            var previousTransactions = new[]
            {
                CreateTransaction(merchantId: "merchant-2", amount: 101),
                CreateTransaction(merchantId: "merchant-3", amount: 102),
                CreateTransaction(merchantId: "merchant-4", amount: 103)
            };

            var context = CreateContext(previousTransactions);

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.True(result.IsTriggered);
            Assert.Equal(10, result.PointsAwarded);
        }

        [Fact]
        public void Evaluate_MoreThanFourMerchantsWithOverallRangeGreaterThanThree_DoesNotTrigger()
        {
            // Arrange
            var rule = new IdenticalAmountAcrossMerchantsRule();

            var previousTransactions = new[]
            {
                CreateTransaction(merchantId: "merchant-2", amount: 101),
                CreateTransaction(merchantId: "merchant-3", amount: 102),
                CreateTransaction(merchantId: "merchant-4", amount: 103),
                CreateTransaction(merchantId: "merchant-5", amount: 104)
            };

            var context = CreateContext(previousTransactions);

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }
    }
}
