namespace Serilog.Sinks.ApplicationInsights.AspNetCore.UnitTests;

[TestFixture]
public sealed class ApplicationInsightsTelemetryConverterTests
{
    private static readonly ApplicationInsightsTelemetryConverter Converter = new();
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [Test]
    public void Convert_unknown_telemetry_type_emits_trace()
    {
        var e = LogEventFactory.Create(LogEventLevel.Warning, null, "x", ("TelemetryType", new ScalarValue("Other")), ("Custom", new ScalarValue("x")));
        var t = Converter.Convert(e, Inv).Single();
        t.Should().BeOfType<TraceTelemetry>();
        var trace = (TraceTelemetry)t;
        trace.SeverityLevel.Should().Be(SeverityLevel.Warning);
        trace.Properties.Should().ContainKey("Custom");
    }

    [Test]
    public void Convert_event_copies_custom_properties_and_metrics()
    {
        var e = LogEventFactory.Create(
            LogEventLevel.Information,
            null,
            "event",
            ("TelemetryType", new ScalarValue(ApplicationInsightsPropertyKeys.TelemetryTypes.Event)),
            ("EventName", new ScalarValue("E1")),
            ("Dim", new ScalarValue("v")),
            (ApplicationInsightsPropertyKeys.EventMetricPrefix + "Hits", new ScalarValue(7)));

        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<EventTelemetry>().Subject;
        t.Name.Should().Be("E1");
        t.Properties.Should().ContainKey("Dim");
        t.Metrics["Hits"].Should().Be(7);
    }

    [Test]
    public void Convert_metric_uses_defaults_when_fields_missing()
    {
        var e = LogEventFactory.Create(
            LogEventLevel.Information,
            null,
            "metric",
            ("TelemetryType", new ScalarValue(ApplicationInsightsPropertyKeys.TelemetryTypes.Metric)));
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<MetricTelemetry>().Subject;
        t.Name.Should().Be("Metric");
        t.Sum.Should().Be(0);
    }

    [Test]
    public void Convert_dependency_sets_result_code_and_duration()
    {
        var e = LogEventFactory.Create(
            LogEventLevel.Information,
            null,
            "dep",
            ("TelemetryType", new ScalarValue(ApplicationInsightsPropertyKeys.TelemetryTypes.Dependency)),
            ("DependencyTypeName", new ScalarValue("HTTP")),
            ("DependencyTarget", new ScalarValue("host")),
            ("DependencyName", new ScalarValue("GET /a")),
            ("DependencyData", new ScalarValue("/a")),
            ("DurationMs", new ScalarValue(15.0)),
            ("Success", new ScalarValue(false)),
            ("DependencyResultCode", new ScalarValue("503")));
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<DependencyTelemetry>().Subject;
        t.Type.Should().Be("HTTP");
        t.Target.Should().Be("host");
        t.Name.Should().Be("GET /a");
        t.Data.Should().Be("/a");
        t.Duration.Should().Be(TimeSpan.FromMilliseconds(15));
        t.Success.Should().BeFalse();
        t.ResultCode.Should().Be("503");
    }

    [Test]
    public void Convert_request_sets_url_and_id_when_valid()
    {
        var e = LogEventFactory.Create(
            LogEventLevel.Information,
            null,
            "req",
            ("TelemetryType", new ScalarValue(ApplicationInsightsPropertyKeys.TelemetryTypes.Request)),
            ("RequestName", new ScalarValue("Op")),
            ("DurationMs", new ScalarValue(1.0)),
            ("ResponseCode", new ScalarValue("201")),
            ("Success", new ScalarValue(true)),
            ("RequestUrl", new ScalarValue("https://a/b")),
            ("RequestId", new ScalarValue("abc")));
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<RequestTelemetry>().Subject;
        t.Name.Should().Be("Op");
        t.ResponseCode.Should().Be("201");
        t.Success.Should().BeTrue();
        t.Url.Should().NotBeNull();
        t.Url!.ToString().Should().Contain("a/b");
        t.Id.Should().Be("abc");
    }

    [Test]
    public void Convert_exception_without_log_exception_uses_message_property()
    {
        var e = LogEventFactory.Create(
            LogEventLevel.Error,
            null,
            "Handled elsewhere",
            ("TelemetryType", new ScalarValue(ApplicationInsightsPropertyKeys.TelemetryTypes.Exception)),
            ("ExceptionMessage", new ScalarValue("Handled elsewhere")));
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<ExceptionTelemetry>().Subject;
        t.Exception.Should().NotBeNull();
        t.Exception.Should().BeOfType<InvalidOperationException>();
        t.Message.Should().Contain("Handled elsewhere");
    }

    [Test]
    public void Convert_exception_uses_log_event_exception_when_present()
    {
        var ex = new ArgumentException("arg");
        var e = LogEventFactory.Create(
            LogEventLevel.Error,
            ex,
            "err",
            ("TelemetryType", new ScalarValue(ApplicationInsightsPropertyKeys.TelemetryTypes.Exception)));
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<ExceptionTelemetry>().Subject;
        t.Exception.Should().BeSameAs(ex);
    }

    [Test]
    public void Convert_truncates_property_values_to_max_length()
    {
        var longVal = new string('z', ApplicationInsightsPropertyKeys.MaxPropertyValueLength + 50);
        var e = LogEventFactory.Create(
            LogEventLevel.Information,
            null,
            "e",
            ("TelemetryType", new ScalarValue(ApplicationInsightsPropertyKeys.TelemetryTypes.Event)),
            ("EventName", new ScalarValue("E")),
            ("Big", new ScalarValue(longVal)));
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<EventTelemetry>().Subject;
        t.Properties["Big"].Length.Should().Be(ApplicationInsightsPropertyKeys.MaxPropertyValueLength);
    }

    [Test]
    public void Convert_trace_maps_log_levels_to_severity()
    {
        foreach (var (level, expected) in new (LogEventLevel level, SeverityLevel sev)[]
                 {
                     (LogEventLevel.Verbose, SeverityLevel.Verbose),
                     (LogEventLevel.Debug, SeverityLevel.Verbose),
                     (LogEventLevel.Information, SeverityLevel.Information),
                     (LogEventLevel.Warning, SeverityLevel.Warning),
                     (LogEventLevel.Error, SeverityLevel.Error),
                     (LogEventLevel.Fatal, SeverityLevel.Critical),
                 })
        {
            var e = LogEventFactory.Create(level, null, "lvl");
            var t = Converter.Convert(e, Inv).Single().Should().BeOfType<TraceTelemetry>().Subject;
            t.SeverityLevel.Should().Be(expected, $"for {level}");
        }
    }

    [Test]
    public void Convert_trace_appends_exception_text()
    {
        var ex = new System.IO.IOException("io");
        var e = LogEventFactory.Create(LogEventLevel.Error, ex, "head", ("Msg", new ScalarValue("ignored")));
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<TraceTelemetry>().Subject;
        t.Message.Should().Contain("head");
        t.Message.Should().Contain("IOException");
    }

    [Test]
    public void Convert_trace_maps_unknown_log_level_to_information_severity()
    {
        var parser = new MessageTemplateParser();
        var e = new LogEvent(
            DateTimeOffset.UtcNow,
            (LogEventLevel)42,
            null,
            parser.Parse("x"),
            Array.Empty<LogEventProperty>());
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<TraceTelemetry>().Subject;
        t.SeverityLevel.Should().Be(SeverityLevel.Information);
    }

    [Test]
    public void Convert_trace_renders_non_scalar_property_via_template()
    {
        var nested = new StructureValue(new[] { new LogEventProperty("N", new ScalarValue(7)) });
        var e = LogEventFactory.Create(
            LogEventLevel.Information,
            null,
            "body",
            ("TelemetryType", new ScalarValue("Other")),
            ("Nested", nested));
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<TraceTelemetry>().Subject;
        t.Properties.Should().ContainKey("Nested");
        t.Properties["Nested"].Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Convert_dependency_omits_result_code_when_not_set()
    {
        var e = LogEventFactory.Create(
            LogEventLevel.Information,
            null,
            "dep",
            ("TelemetryType", new ScalarValue(ApplicationInsightsPropertyKeys.TelemetryTypes.Dependency)),
            ("DependencyTypeName", new ScalarValue("SQL")),
            ("DependencyTarget", new ScalarValue("db")),
            ("DependencyName", new ScalarValue("q")),
            ("DurationMs", new ScalarValue(1.0)),
            ("Success", new ScalarValue("false")));
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<DependencyTelemetry>().Subject;
        t.Success.Should().BeFalse();
        t.ResultCode.Should().BeNullOrEmpty();
    }

    [Test]
    public void Convert_request_defaults_success_true_when_key_missing()
    {
        var e = LogEventFactory.Create(
            LogEventLevel.Information,
            null,
            "req",
            ("TelemetryType", new ScalarValue(ApplicationInsightsPropertyKeys.TelemetryTypes.Request)),
            ("RequestName", new ScalarValue("Op")),
            ("DurationMs", new ScalarValue(1.0)),
            ("ResponseCode", new ScalarValue("200")));
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<RequestTelemetry>().Subject;
        t.Success.Should().BeTrue();
    }

    [Test]
    public void Convert_request_treats_numeric_success_scalar_as_false_unless_string_true()
    {
        var e = LogEventFactory.Create(
            LogEventLevel.Information,
            null,
            "req",
            ("TelemetryType", new ScalarValue(ApplicationInsightsPropertyKeys.TelemetryTypes.Request)),
            ("RequestName", new ScalarValue("Op")),
            ("DurationMs", new ScalarValue(0.0)),
            ("ResponseCode", new ScalarValue("500")),
            ("Success", new ScalarValue(0)));
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<RequestTelemetry>().Subject;
        t.Success.Should().BeFalse();
    }

    [Test]
    public void Convert_request_defaults_success_true_when_success_property_is_not_scalar()
    {
        var weird = new StructureValue(new[] { new LogEventProperty("X", new ScalarValue(1)) });
        var e = LogEventFactory.Create(
            LogEventLevel.Information,
            null,
            "req",
            ("TelemetryType", new ScalarValue(ApplicationInsightsPropertyKeys.TelemetryTypes.Request)),
            ("RequestName", new ScalarValue("Op")),
            ("DurationMs", new ScalarValue(0.0)),
            ("ResponseCode", new ScalarValue("418")),
            ("Success", weird));
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<RequestTelemetry>().Subject;
        t.Success.Should().BeTrue();
    }

    [Test]
    public void Convert_request_skips_url_when_uri_invalid()
    {
        var e = LogEventFactory.Create(
            LogEventLevel.Information,
            null,
            "req",
            ("TelemetryType", new ScalarValue(ApplicationInsightsPropertyKeys.TelemetryTypes.Request)),
            ("RequestName", new ScalarValue("Op")),
            ("DurationMs", new ScalarValue(1.0)),
            ("ResponseCode", new ScalarValue("200")),
            ("Success", new ScalarValue(true)),
            ("RequestUrl", new ScalarValue("http://%%%")),
            ("RequestId", new ScalarValue("id1")));
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<RequestTelemetry>().Subject;
        t.Url.Should().BeNull();
        t.Id.Should().Be("id1");
    }

    [Test]
    public void Convert_request_name_falls_back_to_non_scalar_property_string()
    {
        var nameVal = new StructureValue(new[] { new LogEventProperty("X", new ScalarValue("Y")) });
        var e = LogEventFactory.Create(
            LogEventLevel.Information,
            null,
            "ignored",
            ("TelemetryType", new ScalarValue(ApplicationInsightsPropertyKeys.TelemetryTypes.Request)),
            ("RequestName", nameVal),
            ("DurationMs", new ScalarValue(0.0)),
            ("ResponseCode", new ScalarValue("204")));
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<RequestTelemetry>().Subject;
        t.Name.Should().Be(nameVal.ToString());
    }

    [Test]
    public void Convert_metric_parses_numeric_value_from_string_scalar()
    {
        var e = LogEventFactory.Create(
            LogEventLevel.Information,
            null,
            "m",
            ("TelemetryType", new ScalarValue(ApplicationInsightsPropertyKeys.TelemetryTypes.Metric)),
            ("MetricName", new ScalarValue("M")),
            ("MetricValue", new ScalarValue("12.25")));
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<MetricTelemetry>().Subject;
        t.Sum.Should().Be(12.25);
    }

    [Test]
    public void Convert_dependency_duration_accepts_float_scalar()
    {
        var e = LogEventFactory.Create(
            LogEventLevel.Information,
            null,
            "d",
            ("TelemetryType", new ScalarValue(ApplicationInsightsPropertyKeys.TelemetryTypes.Dependency)),
            ("DependencyTypeName", new ScalarValue("HTTP")),
            ("DependencyTarget", new ScalarValue("t")),
            ("DependencyName", new ScalarValue("n")),
            ("DurationMs", new ScalarValue(3.5f)),
            ("Success", new ScalarValue(true)));
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<DependencyTelemetry>().Subject;
        t.Duration.Should().Be(TimeSpan.FromMilliseconds(3.5));
    }

    [Test]
    public void Convert_exception_without_message_exception_or_property_uses_fallback_text()
    {
        var parser = new MessageTemplateParser();
        var e = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Error,
            null,
            parser.Parse("{Missing}"),
            new[]
            {
                new LogEventProperty("TelemetryType", new ScalarValue(ApplicationInsightsPropertyKeys.TelemetryTypes.Exception)),
            });
        var t = Converter.Convert(e, Inv).Single().Should().BeOfType<ExceptionTelemetry>().Subject;
        t.Exception.Should().NotBeNull();
        t.Message.Should().NotBeNullOrEmpty();
    }
}
