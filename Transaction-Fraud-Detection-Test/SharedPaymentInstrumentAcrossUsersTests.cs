
using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection;
using Transaction_Fraud_Detection_API.FraudDetection.Rules;
using Transaction_Fraud_Detection_API.Models;
using Xunit;

namespace Transaction_Fraud_Detection_Test;

public class SharedPaymentInstrumentAcrossUsersTests
{
    private readonly SharedPaymentInstrumentAcrossUsers _rule = new();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Evaluate_LessThanFourDistinctUsers_DoesNotTriggerRule(
        int previousUsersCount)
    {
        // Arrange
        var context = CreateContext(
            previousUsersCount,
            PaymentAttemptStatus.Succeeded);

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.False(result.IsTriggered);
        Assert.Equal(0, result.PointsAwarded);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(10)]
    public void Evaluate_FourOrMoreDistinctUsers_TriggersRule(
        int previousUsersCount)
    {
        // Arrange
        var context = CreateContext(
            previousUsersCount,
            PaymentAttemptStatus.Succeeded);

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.True(result.IsTriggered);
        Assert.Equal(30, result.PointsAwarded);
    }

    [Theory]
    [InlineData(PaymentAttemptStatus.Succeeded)]
    [InlineData(PaymentAttemptStatus.Failed)]
    public void Evaluate_CurrentAttemptStatus_DoesNotAffectRule(
        PaymentAttemptStatus status)
    {
        // Arrange
        var context = CreateContext(
            previousUsersCount: 3,
            currentAttemptStatus: status);

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.True(result.IsTriggered);
        Assert.Equal(30, result.PointsAwarded);
    }

    [Fact]
    public void Evaluate_EmptyPreviousUsersCollection_DoesNotTriggerRule()
    {
        // Arrange
        var context = new PaymentAttemptEvaluationContext
        {
            CurrentPaymentAttempt = new PaymentAttempt
            {
                Status = PaymentAttemptStatus.Succeeded
            },
            PreviousUserIdsWithSameInstrument = Array.Empty<string>()
        };

        // Act
        var result = _rule.Evaluate(context);

        // Assert
        Assert.False(result.IsTriggered);
        Assert.Equal(0, result.PointsAwarded);
    }

    [Fact]
    public void Rule_RuleCode_ReturnsSharedPaymentInstrumentAcrossUsers()
    {
        // Act
        var ruleCode = _rule.RuleCode;

        // Assert
        Assert.Equal(
            FraudRuleCode.SharedPaymentInstrumentAcrossUsers,
            ruleCode);
    }

    private static PaymentAttemptEvaluationContext CreateContext(
        int previousUsersCount,
        PaymentAttemptStatus currentAttemptStatus)
    {
        var previousUserIds = Enumerable
            .Range(1, previousUsersCount)
            .Select(i => $"user-{i}")
            .ToArray();

        return new PaymentAttemptEvaluationContext
        {
            CurrentPaymentAttempt = new PaymentAttempt
            {
                Status = currentAttemptStatus
            },
            PreviousUserIdsWithSameInstrument = previousUserIds
        };
    }
}
