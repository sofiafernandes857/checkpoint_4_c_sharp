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
[Authorize]
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
    [Authorize(Roles = ApplicationRoles.Employee)]
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
    [Authorize(Roles = ApplicationRoles.Employee)]
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
    [Authorize(Roles = ApplicationRoles.Employee)]
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

    /// <summary>Submits an owned draft.</summary>
    /// <param name="id">Expense identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The submitted expense or a problem response.</returns>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = ApplicationRoles.Employee)]
    public async Task<IActionResult> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _expenses.SubmitAsync(User, id, cancellationToken));
        }
        catch (ExpenseOperationException error)
        {
            return Problem(statusCode: error.StatusCode, title: error.Message);
        }
    }

    /// <summary>Approves a submitted expense owned by another user.</summary>
    /// <param name="id">Expense identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The updated expense or a problem response.</returns>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = ApplicationRoles.Approver)]
    public async Task<IActionResult> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _expenses.ApproveAsync(User, id, cancellationToken));
        }
        catch (ExpenseOperationException error)
        {
            return Problem(statusCode: error.StatusCode, title: error.Message);
        }
    }

    /// <summary>Rejects a submitted expense owned by another user.</summary>
    /// <param name="id">Expense identifier.</param>
    /// <param name="request">Rejection justification.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The updated expense or a problem response.</returns>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = ApplicationRoles.Approver)]
    public async Task<IActionResult> RejectAsync(Guid id, RejectExpenseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _expenses.RejectAsync(User, id, request, cancellationToken));
        }
        catch (ExpenseOperationException error)
        {
            return Problem(statusCode: error.StatusCode, title: error.Message);
        }
    }

    /// <summary>Lists only visible expenses.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The visible expenses or a problem response.</returns>
    [HttpGet]
    [Authorize(Roles = ExpenseAccessRules.ReaderRoles)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _expenses.ListAsync(User, cancellationToken));
        }
        catch (ExpenseOperationException error)
        {
            return Problem(statusCode: error.StatusCode, title: error.Message);
        }
    }

    /// <summary>Returns a visible expense, or 404 when outside the read scope.</summary>
    /// <param name="id">Expense identifier.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The expense or a problem response.</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = ExpenseAccessRules.ReaderRoles)]
    public async Task<IActionResult> DetailAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _expenses.DetailAsync(User, id, cancellationToken));
        }
        catch (ExpenseOperationException error)
        {
            return Problem(statusCode: error.StatusCode, title: error.Message);
        }
    }
}
