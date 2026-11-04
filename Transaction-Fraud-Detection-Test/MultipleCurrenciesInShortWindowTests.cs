
using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection;
using Transaction_Fraud_Detection_API.FraudDetection.Rules;
using Transaction_Fraud_Detection_API.Models;
using Xunit;

namespace Transaction_Fraud_Detection_Test.FraudDetection.Rules;

[Collection("SharedDatabase")]
public class MultipleCurrenciesInShortWindowTests
{
    private readonly MultipleCurrenciesInShortWindow _rule = new();

    private static readonly DateTimeOffset CurrentTimestamp =
        new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_ShouldTrigger_WhenThreeTransactionsHaveAtLeastTwoCurrencies()
    {
        // Arrange
        var current = CreateTransaction(currency: "USD");

        var previousTransactions = new[]
        {
            CreateTransaction(currency: "SAR", timestamp: CurrentTimestamp.AddMinutes(-5)),
            CreateTransaction(currency: "SAR", timestamp: CurrentTimestamp.AddMinutes(-2))
        };

        var context = CreateContext(current, previousTransactions);

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.True(result.IsTriggered);
        Assert.Equal(15, result.PointsAwarded);
    }

    [Fact]
    public void Evaluate_ShouldNotTrigger_WhenThereAreFewerThanThreeTransactions()
    {
        // Arrange
        var current = CreateTransaction(currency: "USD");

        var previousTransactions = new[]
        {
            CreateTransaction(currency: "SAR", timestamp: CurrentTimestamp.AddMinutes(-5))
        };

        var context = CreateContext(current, previousTransactions);

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.False(result.IsTriggered);
        Assert.Equal(0, result.PointsAwarded);
    }

    [Fact]
    public void Evaluate_ShouldNotTrigger_WhenAllTransactionsHaveTheSameCurrency()
    {
        // Arrange
        var current = CreateTransaction(currency: "SAR");

        var previousTransactions = new[]
        {
            CreateTransaction(currency: "SAR", timestamp: CurrentTimestamp.AddMinutes(-5)),
            CreateTransaction(currency: "SAR", timestamp: CurrentTimestamp.AddMinutes(-2))
        };

        var context = CreateContext(current, previousTransactions);

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.False(result.IsTriggered);
        Assert.Equal(0, result.PointsAwarded);
    }

    [Fact]
    public void Evaluate_ShouldTrigger_WhenThereAreMoreThanTwoDistinctCurrencies()
    {
        // Arrange
        var current = CreateTransaction(currency: "EUR");

        var previousTransactions = new[]
        {
            CreateTransaction(currency: "SAR", timestamp: CurrentTimestamp.AddMinutes(-5)),
            CreateTransaction(currency: "USD", timestamp: CurrentTimestamp.AddMinutes(-2))
        };

        var context = CreateContext(current, previousTransactions);

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.True(result.IsTriggered);
        Assert.Equal(15, result.PointsAwarded);
    }

    [Fact]
    public void Evaluate_ShouldTrigger_WhenTransactionIsExactlyTenMinutesOld()
    {
        // Arrange
        var current = CreateTransaction(currency: "USD");

        var previousTransactions = new[]
        {
            CreateTransaction(currency: "SAR", timestamp: CurrentTimestamp.AddMinutes(-10)),
            CreateTransaction(currency: "SAR", timestamp: CurrentTimestamp.AddMinutes(-5))
        };

        var context = CreateContext(current, previousTransactions);

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.True(result.IsTriggered);
        Assert.Equal(15, result.PointsAwarded);
    }

    [Fact]
    public void Evaluate_ShouldNotTrigger_WhenAnOlderTransactionIsOutsideTheWindow()
    {
        // Arrange
        var current = CreateTransaction(currency: "SAR");

        var previousTransactions = new[]
        {
            CreateTransaction(currency: "USD", timestamp: CurrentTimestamp.AddMinutes(-11)),
            CreateTransaction(currency: "SAR", timestamp: CurrentTimestamp.AddMinutes(-5))
        };

        var context = CreateContext(current, previousTransactions);

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.False(result.IsTriggered);
        Assert.Equal(0, result.PointsAwarded);
    }

    [Fact]
    public void Evaluate_ShouldExcludeTransactionsAfterCurrentTimestamp()
    {
        // Arrange
        var current = CreateTransaction(currency: "SAR");

        var previousTransactions = new[]
        {
            CreateTransaction(currency: "USD", timestamp: CurrentTimestamp.AddMinutes(1)),
            CreateTransaction(currency: "SAR", timestamp: CurrentTimestamp.AddMinutes(-5))
        };

        var context = CreateContext(current, previousTransactions);

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.False(result.IsTriggered);
        Assert.Equal(0, result.PointsAwarded);
    }

    [Fact]
    public void Evaluate_ShouldIgnoreTransactionsBelongingToOtherUsers()
    {
        // Arrange
        var current = CreateTransaction(
            userId: "user-1",
            currency: "SAR");

        var previousTransactions = new[]
        {
            CreateTransaction(
                userId: "user-1",
                currency: "SAR",
                timestamp: CurrentTimestamp.AddMinutes(-5)),

            CreateTransaction(
                userId: "user-2",
                currency: "USD",
                timestamp: CurrentTimestamp.AddMinutes(-2))
        };

        var context = CreateContext(current, previousTransactions);

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.False(result.IsTriggered);
        Assert.Equal(0, result.PointsAwarded);
    }

    [Fact]
    public void Evaluate_ShouldTrigger_WhenTransactionsBelongToTheSameMerchant()
    {
        // Arrange
        var current = CreateTransaction(
            currency: "USD",
            merchantId: "merchant-1");

        var previousTransactions = new[]
        {
            CreateTransaction(
                currency: "SAR",
                merchantId: "merchant-1",
                timestamp: CurrentTimestamp.AddMinutes(-5)),

            CreateTransaction(
                currency: "SAR",
                merchantId: "merchant-1",
                timestamp: CurrentTimestamp.AddMinutes(-2))
        };

        var context = CreateContext(current, previousTransactions);

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.True(result.IsTriggered);
        Assert.Equal(15, result.PointsAwarded);
    }

    [Fact]
    public void Evaluate_ShouldTrigger_WhenTransactionsBelongToDifferentMerchants()
    {
        // Arrange
        var current = CreateTransaction(
            currency: "USD",
            merchantId: "merchant-1");

        var previousTransactions = new[]
        {
            CreateTransaction(
                currency: "SAR",
                merchantId: "merchant-2",
                timestamp: CurrentTimestamp.AddMinutes(-5)),

            CreateTransaction(
                currency: "SAR",
                merchantId: "merchant-3",
                timestamp: CurrentTimestamp.AddMinutes(-2))
        };

        var context = CreateContext(current, previousTransactions);

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.True(result.IsTriggered);
        Assert.Equal(15, result.PointsAwarded);
    }

    [Fact]
    public void Evaluate_ShouldTreatCurrencyCodesAsCaseInsensitive()
    {
        // Arrange
        var current = CreateTransaction(currency: "sar");

        var previousTransactions = new[]
        {
            CreateTransaction(currency: "SAR", timestamp: CurrentTimestamp.AddMinutes(-5)),
            CreateTransaction(currency: "USD", timestamp: CurrentTimestamp.AddMinutes(-2))
        };

        var context = CreateContext(current, previousTransactions);

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.True(result.IsTriggered);
        Assert.Equal(15, result.PointsAwarded);
    }

    [Fact]
    public void Evaluate_ShouldIncludeCurrentTransactionInTransactionCount()
    {
        // Arrange
        var current = CreateTransaction(currency: "USD");

        var previousTransactions = new[]
        {
            CreateTransaction(currency: "SAR", timestamp: CurrentTimestamp.AddMinutes(-5)),
            CreateTransaction(currency: "SAR", timestamp: CurrentTimestamp.AddMinutes(-2))
        };

        var context = CreateContext(current, previousTransactions);

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.True(result.IsTriggered);
        Assert.Equal(15, result.PointsAwarded);
    }

    [Fact]
    public void Evaluate_ShouldNotAggregateTransactionAmounts()
    {
        // Arrange
        var current = CreateTransaction(
            currency: "USD",
            amount: 0.01m);

        var previousTransactions = new[]
        {
            CreateTransaction(
                currency: "SAR",
                amount: 0.01m,
                timestamp: CurrentTimestamp.AddMinutes(-5)),

            CreateTransaction(
                currency: "SAR",
                amount: 0.01m,
                timestamp: CurrentTimestamp.AddMinutes(-2))
        };

        var context = CreateContext(current, previousTransactions);

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.True(result.IsTriggered);
        Assert.Equal(15, result.PointsAwarded);
    }

    [Fact]
    public void RuleCode_ShouldReturnMultipleCurrenciesInShortWindow()
    {
        // Act
        var ruleCode = _rule.RuleCode;

        // Assert
        Assert.Equal(
            FraudRuleCode.MultipleCurrenciesInShortWindow,
            ruleCode);
    }

    private static FraudEvaluationContext CreateContext(
        Transaction currentTransaction,
        params Transaction[] previousTransactions)
    {
        return new FraudEvaluationContext
        {
            CurrentTransaction = currentTransaction,
            PreviousTransactions = previousTransactions
        };
    }

    private static Transaction CreateTransaction(
        string userId = "user-1",
        string currency = "SAR",
        string merchantId = "merchant-1",
        decimal amount = 100m,
        DateTimeOffset? timestamp = null)
    {
        return new Transaction
        {
            TransactionId = Guid.NewGuid(),
            UserId = userId,
            Currency = currency,
            MerchantId = merchantId,
            Amount = amount,
            Timestamp = timestamp ?? CurrentTimestamp
        };
    }
}
