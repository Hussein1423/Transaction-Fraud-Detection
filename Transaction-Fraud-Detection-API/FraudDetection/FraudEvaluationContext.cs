using Transaction_Fraud_Detection_API.Enums;
using Transaction_Fraud_Detection_API.Models;
namespace Transaction_Fraud_Detection_API.FraudDetection
{
    public class FraudEvaluationContext
    {
        public required Transaction CurrentTransaction { get; init; }

        public IReadOnlyCollection<Transaction> PreviousTransactions { get; init; } = Array.Empty<Transaction>();

        public IReadOnlyCollection<(AccountChangeType Type, DateTimeOffset Time)> RecentAccountChangeEvents { get; init; }
        = Array.Empty<(AccountChangeType, DateTimeOffset)>();

    }
}
