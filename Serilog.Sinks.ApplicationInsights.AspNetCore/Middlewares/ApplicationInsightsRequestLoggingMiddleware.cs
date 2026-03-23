using Serilog.Sinks.ApplicationInsights.AspNetCore.Helpers;

namespace Serilog.Sinks.ApplicationInsights.AspNetCore.Middlewares;

/// <summary>
/// Emits one Application Insights <c>Request</c> telemetry item per HTTP call via
/// <c>LogAppInsightsRequest</c> (Serilog sink + <c>ApplicationInsightsTelemetryConverter</c>).
/// Place before <c>ErrorHandlerMiddleware</c> so <c>await _next</c> completes after exceptions are handled and
/// <see cref="HttpResponse.StatusCode"/> is set.
/// </summary>
public sealed class ApplicationInsightsRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApplicationInsightsRequestLoggingMiddleware> _logger;

    public ApplicationInsightsRequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<ApplicationInsightsRequestLoggingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (UrlHelper.IsPathExcludedFromValidation(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var statusCode = context.Response.StatusCode;
            var path = context.Request.Path.Value ?? "/";
            var requestName = $"{context.Request.Method} {path}";
            var url = context.Request.GetDisplayUrl();

            _logger.LogAppInsightsRequest(
                requestName,
                sw.Elapsed.TotalMilliseconds,
                responseCode: statusCode.ToString(),
                success: statusCode < StatusCodes.Status400BadRequest,
                url: url,
                requestId: context.TraceIdentifier);

            _logger.LogAppInsightsMetric(
                path,
                sw.Elapsed.TotalMilliseconds,
                new Dictionary<string, object?>{ {"responseCode", statusCode}});
        }
    }
}
