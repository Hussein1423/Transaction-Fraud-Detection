using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Transaction_Fraud_Detection_API.Data;
using Transaction_Fraud_Detection_API.DTOs;
using Transaction_Fraud_Detection_API.Models;

namespace Transaction_Fraud_Detection_API.Controllers;

[ApiController]
[Route("api/mock")]
public class MockController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public MockController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpPost("account-change-events")]
    [ProducesResponseType(typeof(AccountChangeEvent), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAccountChangeEvent([FromBody] CreateMockAccountChangeEventRequestDto request)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }
        if (!await HasTransactionsForUserAsync(request.UserId))
        {
            return NotFound(new
            {
                message = $"No transactions found for user '{request.UserId}'."
            });
        }

        var accountChangeEvent = new AccountChangeEvent
        {
            UserId = request.UserId,
            Type = request.Type,
            Time = DateTimeOffset.UtcNow
        };

        _context.AccountChangeEvents.Add(accountChangeEvent);

        await _context.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, accountChangeEvent);
    }

    private async Task<bool> HasTransactionsForUserAsync(string userId)
    {
        return await _context.Transactions.AnyAsync(transaction => transaction.UserId == userId);
    }
}