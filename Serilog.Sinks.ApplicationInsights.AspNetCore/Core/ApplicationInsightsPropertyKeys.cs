namespace Serilog.Sinks.ApplicationInsights.AspNetCore.Core;

/// <summary>
/// Property keys used when sending custom Application Insights telemetry via ILogger/Serilog.
/// Used by <see cref="ApplicationInsightsTelemetryConverter"/> and <see cref="LoggerApplicationInsightsExtensions"/>.
/// </summary>
public static class ApplicationInsightsPropertyKeys
{
    public const string TelemetryType = "TelemetryType";

    public const string EventName = "EventName";
    /// <summary>Prefix for event metrics: properties "Metric_MyName" are sent as EventTelemetry.Metrics["MyName"].</summary>
    public const string EventMetricPrefix = "Metric_";

    public const string MetricName = "MetricName";
    public const string MetricValue = "MetricValue";

    public const string DependencyTypeName = "DependencyTypeName";
    public const string DependencyTarget = "DependencyTarget";
    public const string DependencyName = "DependencyName";
    public const string DependencyData = "DependencyData";
    public const string DependencyResultCode = "DependencyResultCode";
    public const string DurationMs = "DurationMs";
    public const string Success = "Success";

    public const string RequestName = "RequestName";
    public const string RequestUrl = "RequestUrl";
    public const string RequestId = "RequestId";
    public const string ResponseCode = "ResponseCode";

    /// <summary>When TelemetryType=Exception and no exception object is attached, this property is used as the exception message.</summary>
    public const string ExceptionMessage = "ExceptionMessage";

    public static class TelemetryTypes
    {
        public const string Event = "Event";
        public const string Metric = "Metric";
        public const string Dependency = "Dependency";
        public const string Exception = "Exception";
        public const string Request = "Request";
        public const string Trace = "Trace";
    }

    /// <summary>Application Insights max length for trace/exception message.</summary>
    public const int MaxMessageLength = 32_768;

    /// <summary>Application Insights max length for property value.</summary>
    public const int MaxPropertyValueLength = 8_192;

    /// <summary>
    /// Reserved keys that are not copied to custom dimensions (they are used to build the telemetry item).
    /// </summary>
    public static readonly HashSet<string> ReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        TelemetryType,
        EventName,
        MetricName,
        MetricValue,
        DependencyTypeName,
        DependencyTarget,
        DependencyName,
        DependencyData,
        DependencyResultCode,
        DurationMs,
        Success,
        RequestName,
        RequestUrl,
        RequestId,
        ResponseCode,
        ExceptionMessage
    };
}
