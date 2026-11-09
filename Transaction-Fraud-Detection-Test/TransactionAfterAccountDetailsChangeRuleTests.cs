
using System;
using System.Collections.Generic;
using System.Linq;
using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection;
using Transaction_Fraud_Detection_API.FraudDetection.Rules;
using Transaction_Fraud_Detection_API.Models;
using Xunit;

namespace Transaction_Fraud_Detection_Test
{
    public class TransactionAfterAccountDetailsChangeRuleTests
    {
        public static IEnumerable<object[]> ScoringTestCases =>
            new List<object[]>
            {
                new object[]
                {
                    Array.Empty<AccountChangeType>(),
                    false,
                    0
                },
                new object[]
                {
                    new[] { AccountChangeType.EmailAddress },
                    true,
                    20
                },
                new object[]
                {
                    new[] { AccountChangeType.PhoneNumber },
                    true,
                    30
                },
                new object[]
                {
                    new[] { AccountChangeType.Password },
                    true,
                    35
                },
                new object[]
                {
                    new[]
                    {
                        AccountChangeType.Password,
                        AccountChangeType.Password,
                        AccountChangeType.Password
                    },
                    true,
                    35
                },
                new object[]
                {
                    new[]
                    {
                        AccountChangeType.PhoneNumber,
                        AccountChangeType.EmailAddress
                    },
                    true,
                    40
                },
                new object[]
                {
                    new[]
                    {
                        AccountChangeType.Password,
                        AccountChangeType.EmailAddress
                    },
                    true,
                    45
                },
                new object[]
                {
                    new[]
                    {
                        AccountChangeType.Password,
                        AccountChangeType.PhoneNumber
                    },
                    true,
                    45
                },
                new object[]
                {
                    new[]
                    {
                        AccountChangeType.EmailAddress,
                        AccountChangeType.PhoneNumber,
                        AccountChangeType.Password
                    },
                    true,
                    50
                }
            };

        [Theory]
        [MemberData(nameof(ScoringTestCases))]
        public void Evaluate_AccountChanges_ReturnsExpectedResult(
            AccountChangeType[] eventTypes,
            bool expectedIsTriggered,
            int expectedPoints)
        {
            // Arrange
            var rule = new TransactionAfterAccountDetailsChangeRule();

            var context = new FraudEvaluationContext
            {
                CurrentTransaction = new Transaction(),

                RecentAccountChangeEvents = eventTypes
                    .Select(type => (
                        Type: type,
                        Time: DateTimeOffset.UtcNow))
                    .ToArray()
            };

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.Equal(expectedIsTriggered, result.IsTriggered);
            Assert.Equal(expectedPoints, result.PointsAwarded);
            
        }

        [Fact]
        public void Evaluate_UnsupportedAccountChangeType_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var rule = new TransactionAfterAccountDetailsChangeRule();

            var context = new FraudEvaluationContext
            {
                CurrentTransaction = new Transaction(),

                RecentAccountChangeEvents = new[]
                {
                    (
                        Type: (AccountChangeType)999,
                        Time: DateTimeOffset.UtcNow
                    )
                }
            };

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(
                () => rule.Evaluate(context));
        }
    }
}
