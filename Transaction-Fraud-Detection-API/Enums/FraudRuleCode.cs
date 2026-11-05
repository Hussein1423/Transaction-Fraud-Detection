namespace Transaction_Fraud_Detection_API.Enums;

public enum FraudRuleCode
{
    HighAmount,
    HighVelocity,
    DuplicateLikeTransaction,
    IdenticalAmountAcrossMerchants,
    HighCumulativeSpending,
    MultipleCurrenciesInShortWindow,
    PaymentFailuresFollowedBySuccess,
    SharedPaymentInstrumentAcrossUsers
}