using Transaction_Fraud_Detection_API.FraudDetection.Services;
using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection;
using Transaction_Fraud_Detection_API.FraudDetection.Interfaces;
using Xunit;

namespace Transaction_Fraud_Detection_Test
{
    public class FraudEvaluationServiceTests
    {
        private sealed class FakeFraudRule : IFraudRule
        {
            public FraudRuleCode RuleCode { get; }
            private readonly FraudRuleResult _result;

            public FakeFraudRule(
                FraudRuleCode ruleCode,
                bool isTriggered,
                int pointsAwarded)
            {
                RuleCode = ruleCode;
                _result = new FraudRuleResult(isTriggered, pointsAwarded);
            }

            public FraudRuleResult Evaluate(FraudEvaluationContext context)
            {
                return _result;
            }
        }

        private static FraudEvaluationContext CreateContext()
        {
            return new FraudEvaluationContext
            {
                CurrentTransaction = new Transaction_Fraud_Detection_API.Models.Transaction
                {
                    UserId = "user-1",
                    MerchantId = "merchant-1",
                    Amount = 100,
                    Currency = "SAR",
                    Timestamp = DateTimeOffset.UtcNow
                }
            };
        }

        [Fact]
        public void Evaluate_NoRulesTriggered_ReturnsZeroAndAllow()
        {
            // Arrange
            var rules = new[]
            {
                new FakeFraudRule(FraudRuleCode.HighAmount, false, 0),
                new FakeFraudRule(FraudRuleCode.HighVelocity, false, 0)
            };

            var service = new FraudEvaluationService(rules);

            // Act
            var result = service.Evaluate(CreateContext());

            // Assert
            Assert.Equal(0, result.RiskScore);
            Assert.Equal(FraudDecision.Allow, result.Decision);
        }

        [Fact]
        public void Evaluate_ScoreBelowFifty_ReturnsAllow()
        {
            // Arrange
            var rules = new[]
            {
                new FakeFraudRule(FraudRuleCode.HighAmount, true, 10)
            };

            var service = new FraudEvaluationService(rules);

            // Act
            var result = service.Evaluate(CreateContext());

            // Assert
            Assert.Equal(10, result.RiskScore);
            Assert.Equal(FraudDecision.Allow, result.Decision);
        }

        [Fact]
        public void Evaluate_ScoreAboveFifty_ReturnsFlagForReview()
        {
            // Arrange
            var rules = new[]
            {
                new FakeFraudRule(FraudRuleCode.HighAmount, true, 10),
                new FakeFraudRule(FraudRuleCode.HighVelocity, true, 20),
                new FakeFraudRule(FraudRuleCode.HighVelocity, true, 20),
                new FakeFraudRule(FraudRuleCode.DuplicateLikeTransaction, true, 10)
            };

            var service = new FraudEvaluationService(rules);

            // Act
            var result = service.Evaluate(CreateContext());

            // Assert
            Assert.Equal(60, result.RiskScore);
            Assert.Equal(FraudDecision.FlagForReview, result.Decision);
        }

        [Fact]
        public void Evaluate_ScoreExactlyFifty_ReturnsFlagForReview()
        {
            // Arrange
            var rules = new[]
            {
                new FakeFraudRule(FraudRuleCode.HighAmount, true, 10),
                new FakeFraudRule(FraudRuleCode.HighVelocity, true, 20),
                new FakeFraudRule(FraudRuleCode.HighAmount, true, 10),
                new FakeFraudRule(FraudRuleCode.DuplicateLikeTransaction, true, 10)
            };

            var service = new FraudEvaluationService(rules);

            // Act
            var result = service.Evaluate(CreateContext());

            // Assert
            Assert.Equal(50, result.RiskScore);
            Assert.Equal(FraudDecision.FlagForReview, result.Decision);
        }

        [Fact]
        public void Evaluate_ScoreAboveOneHundred_CapsScoreAtOneHundred()
        {
            // Arrange
            var rules = new[]
            {
                new FakeFraudRule(FraudRuleCode.HighAmount, true, 10),
                new FakeFraudRule(FraudRuleCode.HighVelocity, true, 20),
                new FakeFraudRule(FraudRuleCode.DuplicateLikeTransaction, true, 10),
                 new FakeFraudRule(FraudRuleCode.HighAmount, true, 10),
                new FakeFraudRule(FraudRuleCode.HighVelocity, true, 20),
                new FakeFraudRule(FraudRuleCode.DuplicateLikeTransaction, true, 10),
                 new FakeFraudRule(FraudRuleCode.HighAmount, true, 10),
                new FakeFraudRule(FraudRuleCode.HighVelocity, true, 20),
                new FakeFraudRule(FraudRuleCode.DuplicateLikeTransaction, true, 10)
            };

            var service = new FraudEvaluationService(rules);

            // Act
            var result = service.Evaluate(CreateContext());

            // Assert
            Assert.Equal(100, result.RiskScore);
            Assert.Equal(FraudDecision.FlagForReview, result.Decision);
        }
    }
}
