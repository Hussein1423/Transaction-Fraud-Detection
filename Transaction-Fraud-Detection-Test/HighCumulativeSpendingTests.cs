
using Transaction_Fraud_Detection_API.FraudDetection;
using Transaction_Fraud_Detection_API.Models;
using Xunit;

namespace Transaction_Fraud_Detection_Test
{
    [Collection("SharedDatabase")]
    public class HighCumulativeSpendingTests
    {
        private static readonly DateTimeOffset CurrentTimestamp =
            new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

        private static Transaction CreateTransaction(
            string userId = "user-1",
            decimal amount = 100,
            string currency = "SAR",
            string merchantId = "merchant-1",
            DateTimeOffset? timestamp = null)
        {
            return new Transaction
            {
                UserId = userId,
                Amount = amount,
                Currency = currency,
                MerchantId = merchantId,
                Timestamp = timestamp ?? CurrentTimestamp
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

        // 1. Ten-minute tier

        [Fact]
        public void Evaluate_TenMinuteSpendingExceedsLimit_TriggersRule()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 600),
                CreateTransaction(amount: 500,
                    timestamp: CurrentTimestamp.AddMinutes(-5)));

            var result = rule.Evaluate(context);

            Assert.True(result.IsTriggered);
            Assert.Equal(20, result.PointsAwarded);
        }

        // 2. Thirty-minute tier

        [Fact]
        public void Evaluate_ThirtyMinuteSpendingExceedsLimit_TriggersRule()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 600),
                CreateTransaction(amount: 1001,
                    timestamp: CurrentTimestamp.AddMinutes(-15)));

            var result = rule.Evaluate(context);

            Assert.True(result.IsTriggered);
            Assert.Equal(20, result.PointsAwarded);
        }

        // 3. One-hour tier

        [Fact]
        public void Evaluate_OneHourSpendingExceedsLimit_TriggersRule()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 1000),
                CreateTransaction(amount: 1101,
                    timestamp: CurrentTimestamp.AddMinutes(-45)));

            var result = rule.Evaluate(context);

            Assert.True(result.IsTriggered);
            Assert.Equal(20, result.PointsAwarded);
        }

        // 4. Twenty-four-hour tier

        [Fact]
        public void Evaluate_TwentyFourHourSpendingExceedsLimit_TriggersRule()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 100),
                CreateTransaction(amount: 4501,
                    timestamp: CurrentTimestamp.AddHours(-2)));

            var result = rule.Evaluate(context);

            Assert.True(result.IsTriggered);
            Assert.Equal(20, result.PointsAwarded);
        }

        // 5. Ten-minute threshold boundary

        [Fact]
        public void Evaluate_TenMinuteSpendingEqualsLimit_DoesNotTrigger()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 100),
                CreateTransaction(amount: 900,
                    timestamp: CurrentTimestamp.AddMinutes(-5)));

            var result = rule.Evaluate(context);

            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        // 6. Thirty-minute threshold boundary

        [Fact]
        public void Evaluate_ThirtyMinuteSpendingEqualsLimit_DoesNotTrigger()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 100),
                CreateTransaction(amount: 1400,
                    timestamp: CurrentTimestamp.AddMinutes(-15)));

            var result = rule.Evaluate(context);

            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        // 7. One-hour threshold boundary

        [Fact]
        public void Evaluate_OneHourSpendingEqualsLimit_DoesNotTrigger()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 100),
                CreateTransaction(amount: 1900,
                    timestamp: CurrentTimestamp.AddMinutes(-45)));

            var result = rule.Evaluate(context);

            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        // 8. Twenty-four-hour threshold boundary

        [Fact]
        public void Evaluate_TwentyFourHourSpendingEqualsLimit_DoesNotTrigger()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 100),
                CreateTransaction(amount: 4400,
                    timestamp: CurrentTimestamp.AddHours(-2)));

            var result = rule.Evaluate(context);

            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        // 9. Only current transaction

        [Fact]
        public void Evaluate_OnlyCurrentTransactionExceedsLimit_DoesNotTrigger()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 5000));

            var result = rule.Evaluate(context);

            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        // 10. Two transactions below all thresholds

        [Fact]
        public void Evaluate_TwoTransactionsBelowAllLimits_DoesNotTrigger()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 100),
                CreateTransaction(amount: 200,
                    timestamp: CurrentTimestamp.AddMinutes(-5)));

            var result = rule.Evaluate(context);

            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        // 11. Transactions from another user

        [Fact]
        public void Evaluate_TransactionsFromOtherUsers_DoesNotCountThem()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 100),
                CreateTransaction(
                    userId: "user-2",
                    amount: 5000,
                    timestamp: CurrentTimestamp.AddMinutes(-5)));

            var result = rule.Evaluate(context);

            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        // 12. Transactions using another currency

        [Fact]
        public void Evaluate_TransactionsWithDifferentCurrency_DoesNotCountThem()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 100, currency: "SAR"),
                CreateTransaction(
                    amount: 5000,
                    currency: "USD",
                    timestamp: CurrentTimestamp.AddMinutes(-5)));

            var result = rule.Evaluate(context);

            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        // 13. Transactions from different merchants

        [Fact]
        public void Evaluate_TransactionsFromDifferentMerchants_CountsAllTransactions()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(
                    amount: 600,
                    merchantId: "merchant-1"),
                CreateTransaction(
                    amount: 500,
                    merchantId: "merchant-2",
                    timestamp: CurrentTimestamp.AddMinutes(-5)));

            var result = rule.Evaluate(context);

            Assert.True(result.IsTriggered);
            Assert.Equal(20, result.PointsAwarded);
        }

        // 14. Exact lower boundary of the 10-minute window

        [Fact]
        public void Evaluate_TransactionExactlyTenMinutesEarlier_CountsIt()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 600),
                CreateTransaction(
                    amount: 500,
                    timestamp: CurrentTimestamp.AddMinutes(-10)));

            var result = rule.Evaluate(context);

            Assert.True(result.IsTriggered);
            Assert.Equal(20, result.PointsAwarded);
        }

        // 15. Transaction older than 24 hours

        [Fact]
        public void Evaluate_TransactionOlderThanTwentyFourHours_DoesNotCountIt()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 100),
                CreateTransaction(
                    amount: 5000,
                    timestamp: CurrentTimestamp.AddHours(-24)
                        .AddTicks(-1)));

            var result = rule.Evaluate(context);

            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        // 16. Future transaction

        [Fact]
        public void Evaluate_FutureTransaction_DoesNotCountIt()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 100),
                CreateTransaction(
                    amount: 5000,
                    timestamp: CurrentTimestamp.AddMinutes(1)));

            var result = rule.Evaluate(context);

            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        // 17. Multiple tiers match

        [Fact]
        public void Evaluate_MultipleTiersMatch_AwardsOnlyTwentyPoints()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 2000),
                CreateTransaction(
                    amount: 3000,
                    timestamp: CurrentTimestamp.AddMinutes(-5)));

            var result = rule.Evaluate(context);

            Assert.True(result.IsTriggered);
            Assert.Equal(20, result.PointsAwarded);
        }

        // 18. Shorter tier fails but longer tier matches

        [Fact]
        public void Evaluate_ShorterTierDoesNotMatchButLongerTierMatches_TriggersRule()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 100),
                CreateTransaction(
                    amount: 2000,
                    timestamp: CurrentTimestamp.AddMinutes(-45)));

            var result = rule.Evaluate(context);

            Assert.True(result.IsTriggered);
            Assert.Equal(20, result.PointsAwarded);
        }

        // 19. No tier matches

        [Fact]
        public void Evaluate_NoTierMatches_ReturnsZeroPoints()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 100),
                CreateTransaction(
                    amount: 200,
                    timestamp: CurrentTimestamp.AddHours(-2)));

            var result = rule.Evaluate(context);

            Assert.False(result.IsTriggered);
            Assert.Equal(0, result.PointsAwarded);
        }

        // 20. Transaction exactly at the 24-hour boundary

        [Fact]
        public void Evaluate_TransactionExactlyTwentyFourHoursEarlier_CountsIt()
        {
            var rule = new HighCumulativeSpending();

            var context = CreateContext(
                CreateTransaction(amount: 100),
                CreateTransaction(
                    amount: 4500,
                    timestamp: CurrentTimestamp.AddHours(-24)));

            var result = rule.Evaluate(context);

            Assert.True(result.IsTriggered);
            Assert.Equal(20, result.PointsAwarded);
        }
    }
}
