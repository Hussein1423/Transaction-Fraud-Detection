
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using System.Net;
using Transaction_Fraud_Detection_API.FraudDetection.Interfaces;
using Transaction_Fraud_Detection_API.Interfaces;
using Transaction_Fraud_Detection_API.Models;

namespace Transaction_Fraud_Detection_API.FraudDetection.Services;

public class TransactionLocationService : ITransactionLocationService
{
    private readonly DatabaseReader _databaseReader;

    public TransactionLocationService(DatabaseReader databaseReader)
    {
        _databaseReader = databaseReader;
    }

    public TransactionLocation? Resolve(IPAddress? ipAddress)
    {
        if (ipAddress is null)
            return null;

        try
        {
            var response = _databaseReader.City(ipAddress);

            var city = response.City?.Name;
            var country = response.Country?.Name;

            if (city is null && country is null)
                return null;

            return new TransactionLocation(city, country);
        }
        catch (AddressNotFoundException)
        {
            return null;
        }
    }
}
