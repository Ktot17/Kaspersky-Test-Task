using BLComponent.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WebComponent.ExceptionHandlers;

public sealed class ScanQueueUnavailableExceptionHandler(
    ILogger<ScanQueueUnavailableExceptionHandler> logger) 
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ScanQueueUnavailableException ex)
            return false;

        logger.LogWarning(ex,
            "Scan queue unavailable for {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Scan queue unavailable",
            Detail = ex.Message,
            Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.4"
        }, cancellationToken);

        return true;
    }
}
