using System.Net;
using Transaction_Fraud_Detection_API.Models;

namespace Transaction_Fraud_Detection_API.Interfaces;

public interface ITransactionLocationService
{
    TransactionLocation? Resolve(IPAddress? ipAddress);
}