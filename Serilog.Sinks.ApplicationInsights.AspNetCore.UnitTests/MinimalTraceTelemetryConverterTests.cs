namespace Serilog.Sinks.ApplicationInsights.AspNetCore.UnitTests;

[TestFixture]
public sealed class MinimalTraceTelemetryConverterTests
{
    private static readonly MinimalTraceTelemetryConverter Converter = new();
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [Test]
    public void Convert_emits_single_trace_with_level_mapping()
    {
        var e = LogEventFactory.Create(LogEventLevel.Warning, null, "w");
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<TraceTelemetry>().Subject;
        t.SeverityLevel.Should().Be(SeverityLevel.Warning);
    }

    [Test]
    public void Convert_appends_exception_to_message()
    {
        var ex = new InvalidOperationException("x");
        var e = LogEventFactory.Create(LogEventLevel.Error, ex, "e");
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<TraceTelemetry>().Subject;
        t.Message.Should().Contain("InvalidOperationException");
        t.Message.Should().Contain("x");
    }

    [Test]
    public void Convert_maps_unknown_log_level_to_information()
    {
        var parser = new MessageTemplateParser();
        var e = new LogEvent(
            DateTimeOffset.UtcNow,
            (LogEventLevel)99,
            null,
            parser.Parse("z"),
            Array.Empty<LogEventProperty>());
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<TraceTelemetry>().Subject;
        t.SeverityLevel.Should().Be(SeverityLevel.Information);
    }
}
