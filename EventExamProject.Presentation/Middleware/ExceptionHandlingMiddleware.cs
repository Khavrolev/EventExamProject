using EventExamProject.Domain.Exceptions;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace EventExamProject.Presentation.Middleware;

public class ExceptionHandlingMiddleware
{
    private const string GenericErrorDetail = "An unexpected error occurred. Please try again later.";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception ex)
        {
            await HandleException(httpContext, ex);
        }
    }

    private async Task HandleException(HttpContext httpContext, Exception ex)
    {
        _logger.LogError(
            ex,
            "Unhandled exception. Method={Method}, Path={Path}, RequestId={RequestId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.Request.Headers["x-request-id"]);

        if (httpContext.Response.HasStarted)
        {
            return;
        }

        var statusCode = MapStatusCode(ex);
        var title = MapTitle(ex);

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        var error = new ProblemDetails
        {
            Title = title,
            Status = statusCode,
            // Exception details are logged above; only known, mapped exceptions are safe
            // to echo back to the client — an unmapped (500) exception could leak internals
            // (e.g. raw database error text), so it gets a generic message instead.
            Detail = statusCode == StatusCodes.Status500InternalServerError ? GenericErrorDetail : ex.Message
        };

        await httpContext.Response.WriteAsJsonAsync(error);
    }

    private static int MapStatusCode(Exception ex)
        => ex switch
        {
            ValidationException or DomainValidationException => StatusCodes.Status400BadRequest,
            NotFoundException => StatusCodes.Status404NotFound,
            NoAvailableSeatsException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

    private static string MapTitle(Exception ex)
        => ex switch
        {
            ValidationException or DomainValidationException => "Bad request",
            NotFoundException => "Not Found",
            NoAvailableSeatsException => "No available seats",
            _ => "Internal Server Error",
        };
}
