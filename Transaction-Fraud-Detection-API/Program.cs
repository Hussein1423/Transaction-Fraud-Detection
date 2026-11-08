using Microsoft.EntityFrameworkCore;
using Transaction_Fraud_Detection_API.Data;
using Transaction_Fraud_Detection_API.FraudDetection;
using Transaction_Fraud_Detection_API.FraudDetection.Interfaces;
using Transaction_Fraud_Detection_API.FraudDetection.Rules;
using Transaction_Fraud_Detection_API.FraudDetection.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IFraudEvaluationService, FraudEvaluationService>();

builder.Services.AddScoped<IFraudRule, HighAmountRule>();
builder.Services.AddScoped<IFraudRule, HighVelocityRule>();
builder.Services.AddScoped<IFraudRule, DuplicateLikeTransactionRule>();
builder.Services.AddScoped<IFraudRule, IdenticalAmountAcrossMerchantsRule>();
builder.Services.AddScoped<IFraudRule, HighCumulativeSpending>();
builder.Services.AddScoped<IFraudRule, MultipleCurrenciesInShortWindow>();
builder.Services.AddScoped<IFraudRule, TransactionAfterAccountDetailsChangeRule>();
builder.Services.AddScoped<TransactionEvaluationWorkflowService>();


builder.Services.AddScoped<IPaymentAttemptFraudRule, PaymentFailuresFollowedBySuccess>();
builder.Services.AddScoped<IPaymentAttemptFraudRule, SharedPaymentInstrumentAcrossUsers>();
builder.Services.AddScoped<IPaymentAttemptFraudEvaluationService, PaymentAttemptFraudEvaluationService>();
builder.Services.AddScoped<IPaymentAttemptEvaluationWorkflowService,PaymentAttemptEvaluationWorkflowService>();


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
