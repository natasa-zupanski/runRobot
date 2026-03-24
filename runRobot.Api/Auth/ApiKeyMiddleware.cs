namespace runRobot.Api.Auth;

/// <summary>
/// Rejects requests that do not carry a valid X-Api-Key header.
/// Each key in configuration is bound to a specific machine name, so a key
/// issued for one machine will not work when the server runs on another.
///
/// OpenAPI/Scalar documentation routes are exempt so the API reference
/// remains browsable without a key.
/// </summary>
public class ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
{
    private const string HeaderName = "X-Api-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        // Let documentation routes through unauthenticated.
        if (context.Request.Path.StartsWithSegments("/openapi") ||
            context.Request.Path.StartsWithSegments("/scalar"))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var supplied))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "X-Api-Key header is required." });
            return;
        }

        var currentMachine = Environment.MachineName;
        var entries = config.GetSection("ApiKeys").Get<List<ApiKeyEntry>>() ?? [];

        bool valid = entries.Any(e =>
            e.Key == supplied.ToString() &&
            string.Equals(e.Machine, currentMachine, StringComparison.OrdinalIgnoreCase));

        if (!valid)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or unauthorized API key." });
            return;
        }

        await next(context);
    }
}

public record ApiKeyEntry(string Key, string Machine);
