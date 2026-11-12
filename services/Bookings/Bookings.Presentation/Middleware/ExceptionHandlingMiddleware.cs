using System.ComponentModel.DataAnnotations;
using Bookings.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Bookings.Presentation.Middleware;

public class ExceptionHandlingMiddleware
{
    private const string GenericErrorDetail = "An unexpected error occurred. Please try again later.";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        _logger.LogError(ex, "Unhandled exception. Method={Method}, Path={Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        if (httpContext.Response.HasStarted)
        {
            return;
        }

        var statusCode = ex switch
        {
            ValidationException or DomainValidationException => StatusCodes.Status400BadRequest,
            ForbiddenException => StatusCodes.Status403Forbidden,
            NotFoundException => StatusCodes.Status404NotFound,
            BookingLimitExceededException => StatusCodes.Status409Conflict,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError,
        };

        var title = ex switch
        {
            ValidationException or DomainValidationException => "Bad request",
            ForbiddenException => "Forbidden",
            NotFoundException => "Not Found",
            BookingLimitExceededException => "Booking limit exceeded",
            UnauthorizedAccessException => "Unauthorized",
            _ => "Internal Server Error",
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        var error = new ProblemDetails
        {
            Title = title,
            Status = statusCode,
            Detail = statusCode == StatusCodes.Status500InternalServerError ? GenericErrorDetail : ex.Message
        };

        await httpContext.Response.WriteAsJsonAsync(error);
    }
}
