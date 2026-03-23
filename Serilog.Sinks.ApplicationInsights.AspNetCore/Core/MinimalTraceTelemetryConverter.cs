namespace Serilog.Sinks.ApplicationInsights.AspNetCore.Core;

/// <summary>
/// Converts Serilog log events to Application Insights TraceTelemetry without calling
/// TelemetryConverterBase.PopulateTelemetryFromLogEvent (which touches OperationContext.set_Id
/// and throws MethodAccessException with Microsoft.ApplicationInsights 3.x).
/// </summary>
public sealed class MinimalTraceTelemetryConverter : ITelemetryConverter
{
    public IEnumerable<ITelemetry> Convert(LogEvent logEvent, IFormatProvider formatProvider)
    {
        var message = logEvent.RenderMessage(formatProvider);
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

        var trace = new Microsoft.ApplicationInsights.DataContracts.TraceTelemetry(message, severityLevel);
        if (logEvent.Exception != null)
            trace.Message += Environment.NewLine + logEvent.Exception;

        yield return trace;
    }
}
