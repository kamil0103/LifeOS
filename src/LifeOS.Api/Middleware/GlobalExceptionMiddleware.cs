using System.Net;
using System.Text.Json;

namespace LifeOS.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business rule violation");
            await WriteProblemResponse(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Authentication failure");
            await WriteProblemResponse(context, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await WriteProblemResponse(context, HttpStatusCode.InternalServerError, "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemResponse(HttpContext context, HttpStatusCode statusCode, string detail)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = $"https://httpstatuses.com/{(int)statusCode}",
            title = statusCode.ToString(),
            status = (int)statusCode,
            detail,
            instance = context.Request.Path
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
