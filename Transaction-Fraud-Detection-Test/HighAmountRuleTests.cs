using Transaction_Fraud_Detection_API.FraudDetection;
using Transaction_Fraud_Detection_API.Models;
using Xunit;

namespace Transaction_Fraud_Detection_Test
{
    public class HighAmountRuleTests
    {
        [Theory]
        [InlineData(500, false, 0)]
        [InlineData(1000, false, 0)]
        [InlineData(1001, true, 30)]
        public void Evaluate_TransactionAmount_ReturnsExpectedResult(
            decimal amount,
            bool expectedIsTriggered,
            int expectedPoints)
        {
            // Arrange
            var rule = new HighAmountRule();

            var transaction = new Transaction
            {
                Amount = amount
            };

            var context = new FraudEvaluationContext
            {
                CurrentTransaction = transaction
            };

            // Act
            var result = rule.Evaluate(context);

            // Assert
            Assert.Equal(expectedIsTriggered, result.IsTriggered);
            Assert.Equal(expectedPoints, result.PointsAwarded);
        }
    }
}
