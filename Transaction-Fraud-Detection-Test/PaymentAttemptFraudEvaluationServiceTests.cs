using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.FraudDetection;
using Transaction_Fraud_Detection_API.FraudDetection.Interfaces;
using Transaction_Fraud_Detection_API.FraudDetection.Services;
using Transaction_Fraud_Detection_API.Models;
using Xunit;

namespace Transaction_Fraud_Detection_Test
{
    public class PaymentAttemptFraudEvaluationServiceTests
    {
        [Fact]
        public void Evaluate_NoRulesTriggered_ReturnsZeroScoreAndAllowDecision()
        {
            // Arrange
            var rules = new List<IPaymentAttemptFraudRule>
            {
                new FakePaymentAttemptFraudRule(
                    FraudRuleCode.PaymentFailuresFollowedBySuccess,
                    new FraudRuleResult(false, 0))
            };

            var service = new PaymentAttemptFraudEvaluationService(rules);

            var context = CreateContext();

            // Act
            var result = service.Evaluate(context);

            // Assert
            Assert.Equal(0, result.RiskScore);
            Assert.Equal(FraudDecision.Allow, result.Decision);
            Assert.Empty(result.TriggeredRules);
        }

        [Fact]
        public void Evaluate_OneRuleTriggered_ReturnsItsPoints()
        {
            // Arrange
            var rules = new List<IPaymentAttemptFraudRule>
            {
                new FakePaymentAttemptFraudRule(
                    FraudRuleCode.PaymentFailuresFollowedBySuccess,
                    new FraudRuleResult(true, 20))
            };

            var service = new PaymentAttemptFraudEvaluationService(rules);

            var context = CreateContext();

            // Act
            var result = service.Evaluate(context);

            // Assert
            Assert.Equal(20, result.RiskScore);
            Assert.Equal(FraudDecision.Allow, result.Decision);
            Assert.Single(result.TriggeredRules);

            Assert.Equal(
                FraudRuleCode.PaymentFailuresFollowedBySuccess,
                result.TriggeredRules[0].RuleCode);

            Assert.Equal(20, result.TriggeredRules[0].PointsAwarded);
        }

        [Fact]
        public void Evaluate_MultipleRulesTriggered_SumsTheirPoints()
        {
            // Arrange
            var rules = new List<IPaymentAttemptFraudRule>
            {
                new FakePaymentAttemptFraudRule(
                    FraudRuleCode.PaymentFailuresFollowedBySuccess,
                    new FraudRuleResult(true, 20)),

                new FakePaymentAttemptFraudRule(
                    FraudRuleCode.HighAmount,
                    new FraudRuleResult(true, 30))
            };

            var service = new PaymentAttemptFraudEvaluationService(rules);

            var context = CreateContext();

            // Act
            var result = service.Evaluate(context);

            // Assert
            Assert.Equal(50, result.RiskScore);
            Assert.Equal(FraudDecision.FlagForReview, result.Decision);
            Assert.Equal(2, result.TriggeredRules.Count);
        }

        [Fact]
        public void Evaluate_RiskScoreBelow50_ReturnsAllowDecision()
        {
            // Arrange
            var rules = new List<IPaymentAttemptFraudRule>
            {
                new FakePaymentAttemptFraudRule(
                    FraudRuleCode.PaymentFailuresFollowedBySuccess,
                    new FraudRuleResult(true, 20)),

                new FakePaymentAttemptFraudRule(
                    FraudRuleCode.HighAmount,
                    new FraudRuleResult(true, 20))
            };

            var service = new PaymentAttemptFraudEvaluationService(rules);

            var context = CreateContext();

            // Act
            var result = service.Evaluate(context);

            // Assert
            Assert.Equal(40, result.RiskScore);
            Assert.Equal(FraudDecision.Allow, result.Decision);
        }

        [Fact]
        public void Evaluate_RiskScoreExactly50_ReturnsFlagForReviewDecision()
        {
            // Arrange
            var rules = new List<IPaymentAttemptFraudRule>
            {
                new FakePaymentAttemptFraudRule(
                    FraudRuleCode.PaymentFailuresFollowedBySuccess,
                    new FraudRuleResult(true, 50))
            };

            var service = new PaymentAttemptFraudEvaluationService(rules);

            var context = CreateContext();

            // Act
            var result = service.Evaluate(context);

            // Assert
            Assert.Equal(50, result.RiskScore);
            Assert.Equal(FraudDecision.FlagForReview, result.Decision);
        }

        [Fact]
        public void Evaluate_RiskScoreAbove50_ReturnsFlagForReviewDecision()
        {
            // Arrange
            var rules = new List<IPaymentAttemptFraudRule>
            {
                new FakePaymentAttemptFraudRule(
                    FraudRuleCode.PaymentFailuresFollowedBySuccess,
                    new FraudRuleResult(true, 60))
            };

            var service = new PaymentAttemptFraudEvaluationService(rules);

            var context = CreateContext();

            // Act
            var result = service.Evaluate(context);

            // Assert
            Assert.Equal(60, result.RiskScore);
            Assert.Equal(FraudDecision.FlagForReview, result.Decision);
        }

        [Fact]
        public void Evaluate_TotalPointsExceed100_CapsRiskScoreAt100()
        {
            // Arrange
            var rules = new List<IPaymentAttemptFraudRule>
            {
                new FakePaymentAttemptFraudRule(
                    FraudRuleCode.PaymentFailuresFollowedBySuccess,
                    new FraudRuleResult(true, 60)),

                new FakePaymentAttemptFraudRule(
                    FraudRuleCode.HighAmount,
                    new FraudRuleResult(true, 60))
            };

            var service = new PaymentAttemptFraudEvaluationService(rules);

            var context = CreateContext();

            // Act
            var result = service.Evaluate(context);

            // Assert
            Assert.Equal(100, result.RiskScore);
            Assert.Equal(FraudDecision.FlagForReview, result.Decision);

            // The score is capped, but both triggered rules remain recorded.
            Assert.Equal(2, result.TriggeredRules.Count);
        }

        [Fact]
        public void Evaluate_NoRegisteredRules_ReturnsZeroScoreAndAllowDecision()
        {
            // Arrange
            var rules = new List<IPaymentAttemptFraudRule>();

            var service = new PaymentAttemptFraudEvaluationService(rules);

            var context = CreateContext();

            // Act
            var result = service.Evaluate(context);

            // Assert
            Assert.Equal(0, result.RiskScore);
            Assert.Equal(FraudDecision.Allow, result.Decision);
            Assert.Empty(result.TriggeredRules);
        }

        // --------------------------------------------------
        // Helpers
        // --------------------------------------------------

        private static PaymentAttemptEvaluationContext CreateContext()
        {
            return new PaymentAttemptEvaluationContext
            {
                CurrentPaymentAttempt = new PaymentAttempt
                {
                    PaymentAttemptId = Guid.NewGuid(),
                    Status = PaymentAttemptStatus.Succeeded,
                    AttemptedAt = DateTimeOffset.UtcNow,
                    Transaction = new Transaction
                    {
                        TransactionId = Guid.NewGuid(),
                        UserId = "user-1",
                        Amount = 100,
                        Currency = "SAR",
                        MerchantId = "merchant-1",
                        Timestamp = DateTimeOffset.UtcNow
                    }
                }
            };
        }

        private sealed class FakePaymentAttemptFraudRule
            : IPaymentAttemptFraudRule
        {
            private readonly FraudRuleResult _result;

            public FraudRuleCode RuleCode { get; }

            public FakePaymentAttemptFraudRule(
                FraudRuleCode ruleCode,
                FraudRuleResult result)
            {
                RuleCode = ruleCode;
                _result = result;
            }

            public FraudRuleResult Evaluate(
                PaymentAttemptEvaluationContext context)
            {
                return _result;
            }
        }
    }
}