namespace Serilog.Sinks.ApplicationInsights.AspNetCore.UnitTests.Support;

internal static class LogEventFactory
{
    private static readonly MessageTemplateParser Parser = new();

    public static LogEvent Create(
        LogEventLevel level,
        Exception? exception,
        string messageTemplate,
        params (string Key, LogEventPropertyValue Value)[] props)
    {
        var properties = props.Select(p => new LogEventProperty(p.Key, p.Value)).ToArray();
        return new LogEvent(DateTimeOffset.UtcNow, level, exception, Parser.Parse(messageTemplate), properties);
    }
}
