using System;

namespace ExpenseHub.Api.Services;

/// <summary>A business failure with a consistent HTTP status.</summary>
public sealed class ExpenseOperationException : Exception
{
    /// <summary>Initializes a business failure.</summary>
    /// <param name="statusCode">HTTP status.</param>
    /// <param name="message">Human-readable failure.</param>
    public ExpenseOperationException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>Gets the HTTP status.</summary>
    public int StatusCode { get; }
}
