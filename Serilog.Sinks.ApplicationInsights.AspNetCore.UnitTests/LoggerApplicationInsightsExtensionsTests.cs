namespace Serilog.Sinks.ApplicationInsights.AspNetCore.UnitTests;

[TestFixture]
public sealed class LoggerApplicationInsightsExtensionsTests
{
    [Test]
    public void LogAppInsightsEvent_adds_telemetry_type_event_name_and_custom_properties()
    {
        var sink = new ListLogger<object>();
        Microsoft.Extensions.Logging.ILogger logger = sink;
        var props = new Dictionary<string, object?> { ["Region"] = "eu" };
        var metrics = new Dictionary<string, double> { ["Count"] = 3 };

        logger.LogAppInsightsEvent("OrderPlaced", props, metrics);

        sink.ScopeStates.Should().ContainSingle();
        var map = ToScopeMap(sink.ScopeStates[0]);
        map[ApplicationInsightsPropertyKeys.TelemetryType].Should().Be(ApplicationInsightsPropertyKeys.TelemetryTypes.Event);
        map[ApplicationInsightsPropertyKeys.EventName].Should().Be("OrderPlaced");
        map["Region"].Should().Be("eu");
        map[ApplicationInsightsPropertyKeys.EventMetricPrefix + "Count"].Should().Be(3.0);
        sink.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information);
    }

    [Test]
    public void LogAppInsightsEvent_with_null_optional_dictionaries_sets_only_core_keys()
    {
        var sink = new ListLogger<object>();
        Microsoft.Extensions.Logging.ILogger logger = sink;
        logger.LogAppInsightsEvent("Minimal");

        var map = ToScopeMap(sink.ScopeStates.Single());
        map[ApplicationInsightsPropertyKeys.TelemetryType].Should().Be(ApplicationInsightsPropertyKeys.TelemetryTypes.Event);
        map[ApplicationInsightsPropertyKeys.EventName].Should().Be("Minimal");
        map.Count.Should().Be(2);
    }

    [Test]
    public void LogAppInsightsMetric_sets_metric_fields()
    {
        var sink = new ListLogger<object>();
        Microsoft.Extensions.Logging.ILogger logger = sink;
        logger.LogAppInsightsMetric("Latency", 12.5, new Dictionary<string, object?> { ["Host"] = "a" });

        var map = ToScopeMap(sink.ScopeStates.Single());
        map[ApplicationInsightsPropertyKeys.TelemetryType].Should().Be(ApplicationInsightsPropertyKeys.TelemetryTypes.Metric);
        map[ApplicationInsightsPropertyKeys.MetricName].Should().Be("Latency");
        map[ApplicationInsightsPropertyKeys.MetricValue].Should().Be(12.5);
        map["Host"].Should().Be("a");
    }

    [Test]
    public void LogAppInsightsDependency_uses_type_when_name_null_and_optional_fields()
    {
        var sink = new ListLogger<object>();
        Microsoft.Extensions.Logging.ILogger logger = sink;
        logger.LogAppInsightsDependency("HTTP", "api.example", null, 42, true, data: "/x", resultCode: "200");

        var map = ToScopeMap(sink.ScopeStates.Single());
        map[ApplicationInsightsPropertyKeys.DependencyName].Should().Be("HTTP");
        map[ApplicationInsightsPropertyKeys.DependencyData].Should().Be("/x");
        map[ApplicationInsightsPropertyKeys.DependencyResultCode].Should().Be("200");
    }

    [Test]
    public void LogAppInsightsRequest_defaults_and_optional_url_request_id()
    {
        var sink = new ListLogger<object>();
        Microsoft.Extensions.Logging.ILogger logger = sink;
        logger.LogAppInsightsRequest("GET /", 10, url: "https://x/", requestId: "rid");

        var map = ToScopeMap(sink.ScopeStates.Single());
        map[ApplicationInsightsPropertyKeys.ResponseCode].Should().Be("200");
        map[ApplicationInsightsPropertyKeys.Success].Should().Be(true);
        map[ApplicationInsightsPropertyKeys.RequestUrl].Should().Be("https://x/");
        map[ApplicationInsightsPropertyKeys.RequestId].Should().Be("rid");
    }

    [Test]
    public void LogAppInsightsException_sets_message_property_when_exception_null()
    {
        var sink = new ListLogger<object>();
        Microsoft.Extensions.Logging.ILogger logger = sink;
        logger.LogAppInsightsException(null, message: "Handled");

        var map = ToScopeMap(sink.ScopeStates.Single());
        map[ApplicationInsightsPropertyKeys.ExceptionMessage].Should().Be("Handled");
        sink.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error);
    }

    [Test]
    public void LogAppInsightsException_with_exception_does_not_require_message()
    {
        var sink = new ListLogger<object>();
        Microsoft.Extensions.Logging.ILogger logger = sink;
        var ex = new InvalidOperationException("boom");
        logger.LogAppInsightsException(ex);

        var map = ToScopeMap(sink.ScopeStates.Single());
        map.ContainsKey(ApplicationInsightsPropertyKeys.ExceptionMessage).Should().BeFalse();
        sink.Entries.Single().Message.Should().Contain("boom");
    }

    [Test]
    public void LogAppInsightsDependency_includes_optional_properties_dictionary()
    {
        var sink = new ListLogger<object>();
        Microsoft.Extensions.Logging.ILogger logger = sink;
        logger.LogAppInsightsDependency(
            "HTTP",
            "host",
            "GET",
            5,
            true,
            properties: new Dictionary<string, object?> { ["Lane"] = "primary" });

        var map = ToScopeMap(sink.ScopeStates.Single());
        map["Lane"].Should().Be("primary");
        map.ContainsKey(ApplicationInsightsPropertyKeys.DependencyData).Should().BeFalse();
        map.ContainsKey(ApplicationInsightsPropertyKeys.DependencyResultCode).Should().BeFalse();
    }

    [Test]
    public void LogAppInsightsRequest_includes_optional_properties_dictionary()
    {
        var sink = new ListLogger<object>();
        Microsoft.Extensions.Logging.ILogger logger = sink;
        logger.LogAppInsightsRequest("Job", 1, properties: new Dictionary<string, object?> { ["BatchId"] = "b1" });

        var map = ToScopeMap(sink.ScopeStates.Single());
        map["BatchId"].Should().Be("b1");
        map.ContainsKey(ApplicationInsightsPropertyKeys.RequestUrl).Should().BeFalse();
        map.ContainsKey(ApplicationInsightsPropertyKeys.RequestId).Should().BeFalse();
    }

    [Test]
    public void LogAppInsightsException_with_exception_and_message_uses_both_in_log_call()
    {
        var sink = new ListLogger<object>();
        Microsoft.Extensions.Logging.ILogger logger = sink;
        var ex = new InvalidOperationException("inner");
        logger.LogAppInsightsException(ex, message: "outer context");

        sink.Entries.Single().Message.Should().Contain("outer context");
    }

    private static Dictionary<string, object?> ToScopeMap(object? state)
    {
        state.Should().NotBeNull();
        var s = state!;
        if (s is Dictionary<string, object?> d0)
            return new Dictionary<string, object?>(d0, StringComparer.OrdinalIgnoreCase);
        if (s is Dictionary<string, object> d1)
            return d1.ToDictionary(kv => kv.Key, kv => (object?)kv.Value, StringComparer.OrdinalIgnoreCase);
        throw new InvalidOperationException($"Unexpected scope state: {s.GetType().FullName}");
    }
}
