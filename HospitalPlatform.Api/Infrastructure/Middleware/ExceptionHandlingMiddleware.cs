// Dependencies: validation, exception preservation, JSON, and application types.
using System.ComponentModel.DataAnnotations;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using HospitalPlatform.Api.Infrastructure.Middleware.Exceptions;
using HospitalPlatform.Api.Infrastructure.Middleware.Responses;
using Microsoft.EntityFrameworkCore;

namespace HospitalPlatform.Api.Infrastructure.Middleware;

public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    // 1. Execution: forwards the request and catches exceptions
    // left unhandled by downstream components.
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    // 2. Response: checks whether a response can still be written, classifies the error,
    // and prepares the HTTP status and body for the client.
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            logger.LogWarning(exception, "The response has already started. Exception handling middleware cannot write an error response.");
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        var (statusCode, errorCode, message) = MapException(exception);

        LogException(exception, statusCode);

        var response = new ErrorResponse
        {
            Error = errorCode,
            Message = message,
            StatusCode = statusCode,
            TraceId = context.TraceIdentifier
        };

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await WriteJsonResponseAsync(context, response);
    }

    // 3. Logging: records server failures as errors and expected exceptions
    // as warnings. These logs are separate from the HTTP response.
    private void LogException(Exception exception, int statusCode)
    {
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception.");
            return;
        }

        logger.LogWarning(exception, "Handled application exception.");
    }

    // 4. Serialization: converts ErrorResponse to JSON with camelCase property names
    // and writes the resulting text to the response body.
    private static async Task WriteJsonResponseAsync(HttpContext context, ErrorResponse response)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }

    // 5. Classification: selects the status, code, and message for each exception.
    // Unknown error details are exposed only in Development.
    private (int StatusCode, string ErrorCode, string Message) MapException(Exception exception)
    {
        return exception switch
        {
            AppException appException => (appException.StatusCode, appException.errorCode, appException.Message),
            ValidationException => (StatusCodes.Status400BadRequest, "validation_error", exception.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "concurrency_conflict", "The resource was modified by another operation."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "internal_server_error",
                environment.IsDevelopment() ? exception.Message : "An unexpected error occurred.")
        };
    }
}
