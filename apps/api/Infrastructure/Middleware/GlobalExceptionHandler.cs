using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace T4L.VideoSearch.Api.Infrastructure.Middleware;

/// <summary>
/// Global exception handler for consistent error responses across the API
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        var (statusCode, title, detail) = exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                "Validation Error",
                string.Join("; ", validationEx.Errors.Select(e => e.ErrorMessage))
            ),
            ArgumentNullException argNullEx => (
                StatusCodes.Status400BadRequest,
                "Invalid Request",
                $"Required parameter '{argNullEx.ParamName}' was not provided"
            ),
            ArgumentException argEx => (
                StatusCodes.Status400BadRequest,
                "Invalid Request",
                argEx.Message
            ),
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "You are not authorized to perform this action"
            ),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "Not Found",
                "The requested resource was not found"
            ),
            FileNotFoundException => (
                StatusCodes.Status404NotFound,
                "Not Found",
                "The requested file was not found"
            ),
            InvalidOperationException invalidOpEx => (
                StatusCodes.Status409Conflict,
                "Operation Failed",
                invalidOpEx.Message
            ),
            NotSupportedException notSupEx => (
                StatusCodes.Status400BadRequest,
                "Not Supported",
                notSupEx.Message
            ),
            OperationCanceledException => (
                StatusCodes.Status499ClientClosedRequest, // Custom status for cancelled
                "Request Cancelled",
                "The request was cancelled by the client"
            ),
            TimeoutException => (
                StatusCodes.Status504GatewayTimeout,
                "Timeout",
                "The operation timed out"
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                _environment.IsDevelopment()
                    ? exception.Message
                    : "An unexpected error occurred. Please try again later."
            )
        };

        // Log the exception with appropriate level
        if (statusCode >= 500)
        {
            _logger.LogError(
                exception,
                "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}",
                traceId,
                httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(
                "Request failed with status {StatusCode}. TraceId: {TraceId}, Path: {Path}, Error: {Error}",
                statusCode,
                traceId,
                httpContext.Request.Path,
                detail);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Type = GetRfcType(statusCode)
        };

        problemDetails.Extensions["traceId"] = traceId;
        problemDetails.Extensions["timestamp"] = DateTime.UtcNow;

        // Include stack trace in development
        if (_environment.IsDevelopment() && statusCode >= 500)
        {
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
            problemDetails.Extensions["innerException"] = exception.InnerException?.Message;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static string GetRfcType(int statusCode) => statusCode switch
    {
        400 => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        401 => "https://tools.ietf.org/html/rfc7235#section-3.1",
        403 => "https://tools.ietf.org/html/rfc7231#section-6.5.3",
        404 => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
        409 => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
        422 => "https://tools.ietf.org/html/rfc4918#section-11.2",
        500 => "https://tools.ietf.org/html/rfc7231#section-6.6.1",
        504 => "https://tools.ietf.org/html/rfc7231#section-6.6.5",
        _ => "https://tools.ietf.org/html/rfc7231"
    };
}

/// <summary>
/// Custom status code for client closed request (nginx standard)
/// </summary>
public static class StatusCodes499
{
    public const int Status499ClientClosedRequest = 499;
}

/// <summary>
/// Standard API response wrapper for consistent responses
/// </summary>
/// <typeparam name="T">The type of data being returned</typeparam>
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public string? TraceId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message,
        TraceId = Activity.Current?.Id
    };

    public static ApiResponse<T> Fail(string message) => new()
    {
        Success = false,
        Message = message,
        TraceId = Activity.Current?.Id
    };
}

/// <summary>
/// Non-generic API response for operations without data
/// </summary>
public class ApiResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? TraceId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static ApiResponse Ok(string? message = null) => new()
    {
        Success = true,
        Message = message,
        TraceId = Activity.Current?.Id
    };

    public static ApiResponse Fail(string message) => new()
    {
        Success = false,
        Message = message,
        TraceId = Activity.Current?.Id
    };
}
