namespace Serilog.Sinks.ApplicationInsights.AspNetCore.Core;

/// <summary>
/// Converts Serilog log events to Application Insights telemetry. Supports Event, Metric, Dependency,
/// Exception, Request, and Trace (default) when the log event includes the corresponding
/// <see cref="ApplicationInsightsPropertyKeys.TelemetryType"/> and type-specific properties.
/// Falls back to TraceTelemetry when no type is set. Enforces AI message/property length limits.
/// </summary>
public sealed class ApplicationInsightsTelemetryConverter : ITelemetryConverter
{
    public IEnumerable<ITelemetry> Convert(LogEvent logEvent, IFormatProvider formatProvider)
    {
        var telemetryType = GetString(logEvent, ApplicationInsightsPropertyKeys.TelemetryType);

        return telemetryType switch
        {
            ApplicationInsightsPropertyKeys.TelemetryTypes.Event => new[] { CreateEventTelemetry(logEvent, formatProvider) },
            ApplicationInsightsPropertyKeys.TelemetryTypes.Metric => new[] { CreateMetricTelemetry(logEvent, formatProvider) },
            ApplicationInsightsPropertyKeys.TelemetryTypes.Dependency => new[] { CreateDependencyTelemetry(logEvent, formatProvider) },
            ApplicationInsightsPropertyKeys.TelemetryTypes.Exception => new[] { CreateExceptionTelemetry(logEvent, formatProvider) },
            ApplicationInsightsPropertyKeys.TelemetryTypes.Request => new[] { CreateRequestTelemetry(logEvent, formatProvider) },
            _ => new[] { CreateTraceTelemetry(logEvent, formatProvider) }
        };
    }

    private static ITelemetry CreateEventTelemetry(LogEvent logEvent, IFormatProvider formatProvider)
    {
        var name = Truncate(GetString(logEvent, ApplicationInsightsPropertyKeys.EventName)
            ?? logEvent.RenderMessage(formatProvider)
            ?? "Event", ApplicationInsightsPropertyKeys.MaxMessageLength);
        var eventTelemetry = new Microsoft.ApplicationInsights.DataContracts.EventTelemetry(name);
        CopyToProperties(logEvent, eventTelemetry.Properties, excludePrefix: ApplicationInsightsPropertyKeys.EventMetricPrefix);
        FillEventMetrics(logEvent, eventTelemetry);
        return eventTelemetry;
    }

    private static void FillEventMetrics(LogEvent logEvent, Microsoft.ApplicationInsights.DataContracts.EventTelemetry eventTelemetry)
    {
        var prefix = ApplicationInsightsPropertyKeys.EventMetricPrefix;
        foreach (var prop in logEvent.Properties)
        {
            if (!prop.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            var metricName = prop.Key.Length > prefix.Length ? prop.Key[prefix.Length..] : prop.Key;
            var value = GetDoubleFromValue(prop.Value);
            if (metricName.Length > 0)
                eventTelemetry.Metrics[metricName] = value;
        }
    }

    private static ITelemetry CreateMetricTelemetry(LogEvent logEvent, IFormatProvider formatProvider)
    {
        var name = GetString(logEvent, ApplicationInsightsPropertyKeys.MetricName) ?? "Metric";
        var value = GetDouble(logEvent, ApplicationInsightsPropertyKeys.MetricValue);
        var metricTelemetry = new Microsoft.ApplicationInsights.DataContracts.MetricTelemetry(name, value);
        CopyToProperties(logEvent, metricTelemetry.Properties);
        return metricTelemetry;
    }

    private static ITelemetry CreateDependencyTelemetry(LogEvent logEvent, IFormatProvider formatProvider)
    {
        var typeName = GetString(logEvent, ApplicationInsightsPropertyKeys.DependencyTypeName) ?? "Dependency";
        var target = GetString(logEvent, ApplicationInsightsPropertyKeys.DependencyTarget) ?? "";
        var name = GetString(logEvent, ApplicationInsightsPropertyKeys.DependencyName) ?? logEvent.RenderMessage(formatProvider) ?? typeName;
        var data = GetString(logEvent, ApplicationInsightsPropertyKeys.DependencyData);
        var duration = TimeSpan.FromMilliseconds(GetDouble(logEvent, ApplicationInsightsPropertyKeys.DurationMs));
        var success = GetBool(logEvent, ApplicationInsightsPropertyKeys.Success);
        var resultCode = GetString(logEvent, ApplicationInsightsPropertyKeys.DependencyResultCode);

        var dependencyTelemetry = new Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry(typeName, target, name, data)
        {
            Duration = duration,
            Success = success
        };
        if (!string.IsNullOrEmpty(resultCode))
            dependencyTelemetry.ResultCode = resultCode;
        CopyToProperties(logEvent, dependencyTelemetry.Properties);
        return dependencyTelemetry;
    }

    private static ITelemetry CreateExceptionTelemetry(LogEvent logEvent, IFormatProvider formatProvider)
    {
        var message = Truncate(logEvent.RenderMessage(formatProvider) ?? GetString(logEvent, ApplicationInsightsPropertyKeys.ExceptionMessage), ApplicationInsightsPropertyKeys.MaxMessageLength);
        Exception? ex = logEvent.Exception;
        if (ex == null && !string.IsNullOrEmpty(message))
            ex = new InvalidOperationException(message);

        var exceptionTelemetry = ex != null
            ? new Microsoft.ApplicationInsights.DataContracts.ExceptionTelemetry(ex) { Message = message ?? ex.Message }
            : new Microsoft.ApplicationInsights.DataContracts.ExceptionTelemetry(new InvalidOperationException(message ?? "Exception")) { Message = message ?? "Exception" };
        CopyToProperties(logEvent, exceptionTelemetry.Properties);
        return exceptionTelemetry;
    }

    private static ITelemetry CreateRequestTelemetry(LogEvent logEvent, IFormatProvider formatProvider)
    {
        var name = GetString(logEvent, ApplicationInsightsPropertyKeys.RequestName) ?? logEvent.RenderMessage(formatProvider) ?? "Request";
        var duration = TimeSpan.FromMilliseconds(GetDouble(logEvent, ApplicationInsightsPropertyKeys.DurationMs));
        var responseCode = GetString(logEvent, ApplicationInsightsPropertyKeys.ResponseCode) ?? "200";
        var success = GetBool(logEvent, ApplicationInsightsPropertyKeys.Success);
        var url = GetString(logEvent, ApplicationInsightsPropertyKeys.RequestUrl);
        var id = GetString(logEvent, ApplicationInsightsPropertyKeys.RequestId);

        var startTime = DateTimeOffset.UtcNow - duration;
        var requestTelemetry = new Microsoft.ApplicationInsights.DataContracts.RequestTelemetry(name, startTime, duration, responseCode, success);
        if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
            requestTelemetry.Url = uri;
        if (!string.IsNullOrEmpty(id))
            requestTelemetry.Id = id;
        CopyToProperties(logEvent, requestTelemetry.Properties);
        return requestTelemetry;
    }

    private static ITelemetry CreateTraceTelemetry(LogEvent logEvent, IFormatProvider formatProvider)
    {
        var rawMessage = logEvent.RenderMessage(formatProvider) ?? "";
        if (logEvent.Exception != null)
            rawMessage += (rawMessage.Length > 0 ? Environment.NewLine : "") + logEvent.Exception;
        var message = Truncate(rawMessage, ApplicationInsightsPropertyKeys.MaxMessageLength);

        var severityLevel = logEvent.Level switch
        {
            LogEventLevel.Verbose => Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose,
            LogEventLevel.Debug => Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose,
            LogEventLevel.Information => Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Information,
            LogEventLevel.Warning => Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning,
            LogEventLevel.Error => Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error,
            LogEventLevel.Fatal => Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Critical,
            _ => Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Information
        };

        var trace = new Microsoft.ApplicationInsights.DataContracts.TraceTelemetry(message ?? "", severityLevel);
        CopyToProperties(logEvent, trace.Properties);
        return trace;
    }

    private static void CopyToProperties(LogEvent logEvent, IDictionary<string, string> target, string? excludePrefix = null)
    {
        foreach (var prop in logEvent.Properties)
        {
            if (ApplicationInsightsPropertyKeys.ReservedKeys.Contains(prop.Key))
                continue;
            if (excludePrefix != null && prop.Key.StartsWith(excludePrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            var value = RenderPropertyValue(prop.Value);
            if (value != null)
                target[prop.Key] = Truncate(value, ApplicationInsightsPropertyKeys.MaxPropertyValueLength);
        }
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? RenderPropertyValue(LogEventPropertyValue value)
    {
        if (value is ScalarValue sv)
            return sv.Value?.ToString();
        using var sw = new StringWriter();
        value.Render(sw);
        return sw.ToString();
    }

    private static string? GetString(LogEvent logEvent, string key)
    {
        if (!logEvent.Properties.TryGetValue(key, out var pv))
            return null;
        if (pv is ScalarValue sv)
            return sv.Value?.ToString();
        return pv.ToString();
    }

    private static double GetDouble(LogEvent logEvent, string key)
    {
        if (!logEvent.Properties.TryGetValue(key, out var pv))
            return 0;
        return GetDoubleFromValue(pv);
    }

    private static double GetDoubleFromValue(LogEventPropertyValue pv)
    {
        if (pv is ScalarValue sv && sv.Value != null)
        {
            if (sv.Value is double d) return d;
            if (sv.Value is int i) return i;
            if (sv.Value is long l) return l;
            if (sv.Value is float f) return f;
            if (double.TryParse(sv.Value.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }
        return 0;
    }

    private static bool GetBool(LogEvent logEvent, string key)
    {
        if (!logEvent.Properties.TryGetValue(key, out var pv))
            return true;
        if (pv is ScalarValue sv && sv.Value is bool b)
            return b;
        if (pv is ScalarValue sv2 && sv2.Value != null)
            return string.Equals(sv2.Value.ToString(), "true", StringComparison.OrdinalIgnoreCase);
        return true;
    }
}
