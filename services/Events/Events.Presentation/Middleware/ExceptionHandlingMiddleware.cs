using System.ComponentModel.DataAnnotations;
using Events.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Events.Presentation.Middleware;

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
        _logger.LogError(ex, "Unhandled exception. TraceId={TraceId}, Method={Method}, Path={Path}",
            httpContext.TraceIdentifier, httpContext.Request.Method, httpContext.Request.Path);

        if (httpContext.Response.HasStarted)
        {
            return;
        }

        var statusCode = ex switch
        {
            ValidationException or DomainValidationException => StatusCodes.Status400BadRequest,
            ForbiddenException => StatusCodes.Status403Forbidden,
            NotFoundException => StatusCodes.Status404NotFound,
            DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        var title = ex switch
        {
            ValidationException or DomainValidationException => "Bad request",
            ForbiddenException => "Forbidden",
            NotFoundException => "Not Found",
            DbUpdateConcurrencyException => "Conflict",
            _ => "Internal Server Error",
        };

        var detail = ex switch
        {
            DbUpdateConcurrencyException => "The event was modified or deleted by another concurrent transaction. Please reload and try again.",
            _ => statusCode == StatusCodes.Status500InternalServerError ? GenericErrorDetail : ex.Message
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var error = new ProblemDetails
        {
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        error.Extensions["traceId"] = httpContext.TraceIdentifier;

        await httpContext.Response.WriteAsJsonAsync(error, options: null, contentType: "application/problem+json");
    }
}
