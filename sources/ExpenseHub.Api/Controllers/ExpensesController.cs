using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Security;
using ExpenseHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Controllers;

/// <summary>Expense draft endpoints protected by role and service rules.</summary>
[ApiController]
[Route("api/expenses")]
[Authorize(Roles = ApplicationRoles.Employee)]
public sealed class ExpensesController : ControllerBase
{
    private readonly ExpenseService _expenses;

    /// <summary>Initializes the controller.</summary>
    /// <param name="expenses">Scoped expense service.</param>
    public ExpensesController(ExpenseService expenses)
    {
        _expenses = expenses;
    }

    /// <summary>Creates a draft owned by the authenticated employee.</summary>
    /// <param name="request">Editable fields.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The created draft or a problem response.</returns>
    [HttpPost]
    public async Task<IActionResult> CreateAsync(ExpenseDraftRequest request, CancellationToken cancellationToken)
    {
        try
        {
            Expense expense = await _expenses.CreateAsync(User, request, cancellationToken);
            return Created($"/api/expenses/{expense.Id}", expense);
        }
        catch (ExpenseOperationException error)
        {
            return Problem(statusCode: error.StatusCode, title: error.Message);
        }
    }

    /// <summary>Edits an owned draft.</summary>
    /// <param name="id">Expense identifier.</param>
    /// <param name="request">Editable fields.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The updated draft or a problem response.</returns>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> EditAsync(Guid id, ExpenseDraftRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _expenses.EditAsync(User, id, request, cancellationToken));
        }
        catch (ExpenseOperationException error)
        {
            return Problem(statusCode: error.StatusCode, title: error.Message);
        }
    }

    /// <summary>Lists the categories accepted when creating or editing a draft.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Available categories.</returns>
    [HttpGet("/api/expense-categories")]
    public async Task<IActionResult> CategoriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _expenses.CategoriesAsync(User, cancellationToken));
        }
        catch (ExpenseOperationException error)
        {
            return Problem(statusCode: error.StatusCode, title: error.Message);
        }
    }
}
