namespace Serilog.Sinks.ApplicationInsights.AspNetCore.Core;

/// <summary>
/// ILogger extensions to send Application Insights Event, Metric, Dependency, and Request telemetry
/// via Serilog (use Serilog.Sinks.ApplicationInsights with <see cref="ApplicationInsightsTelemetryConverter"/>).
/// For custom operations (e.g. background jobs) where you want full correlation and a single request in the portal,
/// inject <see cref="Microsoft.ApplicationInsights.TelemetryClient"/> and use
/// <c>telemetryClient.StartOperation&lt;RequestTelemetry&gt;("OperationName")</c>; set properties on
/// <c>operation.Telemetry</c> and <c>operation.Telemetry.Success</c> / <c>ResponseCode</c> on failure.
/// </summary>
public static class LoggerApplicationInsightsExtensions
{
    /// <summary>
    /// Sends an Application Insights custom event. Appears in Application Insights as EventTelemetry.
    /// Use <paramref name="metrics"/> for numeric measures (e.g. Count, Duration); they appear in EventTelemetry.Metrics.
    /// </summary>
    /// <param name="logger">The logger (Serilog-backed).</param>
    /// <param name="eventName">Event name (e.g. "OrderSubmitted").</param>
    /// <param name="properties">Optional properties to attach to the event.</param>
    /// <param name="metrics">Optional metrics (e.g. "Count" = 5). Sent as EventTelemetry.Metrics.</param>
    public static void LogAppInsightsEvent(
        this Microsoft.Extensions.Logging.ILogger logger,
        string eventName,
        IReadOnlyDictionary<string, object?>? properties = null,
        IReadOnlyDictionary<string, double>? metrics = null)
    {
        var state = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [ApplicationInsightsPropertyKeys.TelemetryType] = ApplicationInsightsPropertyKeys.TelemetryTypes.Event,
            [ApplicationInsightsPropertyKeys.EventName] = eventName
        };
        if (properties != null)
        {
            foreach (var kv in properties)
                state[kv.Key] = kv.Value;
        }
        if (metrics != null)
        {
            foreach (var kv in metrics)
                state[ApplicationInsightsPropertyKeys.EventMetricPrefix + kv.Key] = kv.Value;
        }
        using (logger.BeginScope(state))
            logger.LogInformation("Application Insights Event: {EventName}", eventName);
    }

    /// <summary>
    /// Sends an Application Insights metric. Appears in Application Insights as MetricTelemetry.
    /// </summary>
    /// <param name="logger">The logger (Serilog-backed).</param>
    /// <param name="metricName">Metric name (e.g. "OrdersPerMinute").</param>
    /// <param name="value">Numeric value.</param>
    /// <param name="properties">Optional properties to attach.</param>
    public static void LogAppInsightsMetric(
        this Microsoft.Extensions.Logging.ILogger logger,
        string metricName,
        double value,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        var state = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [ApplicationInsightsPropertyKeys.TelemetryType] = ApplicationInsightsPropertyKeys.TelemetryTypes.Metric,
            [ApplicationInsightsPropertyKeys.MetricName] = metricName,
            [ApplicationInsightsPropertyKeys.MetricValue] = value
        };
        if (properties != null)
        {
            foreach (var kv in properties)
                state[kv.Key] = kv.Value;
        }
        using (logger.BeginScope(state))
            logger.LogInformation("Application Insights Metric: {MetricName} = {MetricValue}", metricName, value);
    }

    /// <summary>
    /// Sends an Application Insights dependency. Appears as DependencyTelemetry (e.g. outgoing HTTP/call).
    /// </summary>
    /// <param name="logger">The logger (Serilog-backed).</param>
    /// <param name="dependencyTypeName">Type of dependency (e.g. "HTTP", "SQL").</param>
    /// <param name="target">Target (e.g. host or resource name).</param>
    /// <param name="name">Dependency name (optional).</param>
    /// <param name="durationMs">Duration in milliseconds.</param>
    /// <param name="success">Whether the call succeeded.</param>
    /// <param name="data">Optional command/text (e.g. SQL text, URL path).</param>
    /// <param name="resultCode">Optional result/status code (e.g. HTTP status).</param>
    /// <param name="properties">Optional properties to attach.</param>
    public static void LogAppInsightsDependency(
        this Microsoft.Extensions.Logging.ILogger logger,
        string dependencyTypeName,
        string target,
        string? name,
        double durationMs,
        bool success,
        string? data = null,
        string? resultCode = null,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        var state = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [ApplicationInsightsPropertyKeys.TelemetryType] = ApplicationInsightsPropertyKeys.TelemetryTypes.Dependency,
            [ApplicationInsightsPropertyKeys.DependencyTypeName] = dependencyTypeName,
            [ApplicationInsightsPropertyKeys.DependencyTarget] = target,
            [ApplicationInsightsPropertyKeys.DependencyName] = name ?? dependencyTypeName,
            [ApplicationInsightsPropertyKeys.DurationMs] = durationMs,
            [ApplicationInsightsPropertyKeys.Success] = success
        };
        if (!string.IsNullOrEmpty(data))
            state[ApplicationInsightsPropertyKeys.DependencyData] = data;
        if (!string.IsNullOrEmpty(resultCode))
            state[ApplicationInsightsPropertyKeys.DependencyResultCode] = resultCode;
        if (properties != null)
        {
            foreach (var kv in properties)
                state[kv.Key] = kv.Value;
        }
        using (logger.BeginScope(state))
            logger.LogInformation("Application Insights Dependency: {DependencyTypeName} {DependencyTarget} ({DurationMs}ms)", dependencyTypeName, target, durationMs);
    }

    /// <summary>
    /// Sends an Application Insights request (e.g. for a background job or worker). Appears as RequestTelemetry.
    /// </summary>
    /// <param name="logger">The logger (Serilog-backed).</param>
    /// <param name="requestName">Request/operation name.</param>
    /// <param name="durationMs">Duration in milliseconds.</param>
    /// <param name="responseCode">Response or status code (e.g. "200").</param>
    /// <param name="success">Whether the request succeeded.</param>
    /// <param name="url">Optional request URL (for correlation in portal).</param>
    /// <param name="requestId">Optional operation/request ID (for correlation).</param>
    /// <param name="properties">Optional properties to attach.</param>
    public static void LogAppInsightsRequest(
        this Microsoft.Extensions.Logging.ILogger logger,
        string requestName,
        double durationMs,
        string responseCode = "200",
        bool success = true,
        string? url = null,
        string? requestId = null,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        var state = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [ApplicationInsightsPropertyKeys.TelemetryType] = ApplicationInsightsPropertyKeys.TelemetryTypes.Request,
            [ApplicationInsightsPropertyKeys.RequestName] = requestName,
            [ApplicationInsightsPropertyKeys.DurationMs] = durationMs,
            [ApplicationInsightsPropertyKeys.ResponseCode] = responseCode,
            [ApplicationInsightsPropertyKeys.Success] = success
        };
        if (!string.IsNullOrEmpty(url))
            state[ApplicationInsightsPropertyKeys.RequestUrl] = url;
        if (!string.IsNullOrEmpty(requestId))
            state[ApplicationInsightsPropertyKeys.RequestId] = requestId;
        if (properties != null)
        {
            foreach (var kv in properties)
                state[kv.Key] = kv.Value;
        }
        using (logger.BeginScope(state))
            logger.LogInformation("Application Insights Request: {RequestName} ({DurationMs}ms)", requestName, durationMs);
    }

    /// <summary>
    /// Sends an Application Insights exception telemetry. Use when you catch and handle (or rethrow) an exception.
    /// Pass the exception and set TelemetryType so the converter emits ExceptionTelemetry.
    /// </summary>
    /// <param name="logger">The logger (Serilog-backed).</param>
    /// <param name="exception">The exception (can be null if only <paramref name="message"/> is provided).</param>
    /// <param name="message">Optional message; used as exception message when exception is null.</param>
    /// <param name="properties">Optional properties to attach.</param>
    public static void LogAppInsightsException(
        this Microsoft.Extensions.Logging.ILogger logger,
        Exception? exception,
        string? message = null,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        var state = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [ApplicationInsightsPropertyKeys.TelemetryType] = ApplicationInsightsPropertyKeys.TelemetryTypes.Exception
        };
        if (exception == null && !string.IsNullOrEmpty(message))
            state[ApplicationInsightsPropertyKeys.ExceptionMessage] = message;
        if (properties != null)
        {
            foreach (var kv in properties)
                state[kv.Key] = kv.Value;
        }
        using (logger.BeginScope(state))
            logger.LogError(exception, message ?? exception?.Message ?? "Exception");
    }
}
