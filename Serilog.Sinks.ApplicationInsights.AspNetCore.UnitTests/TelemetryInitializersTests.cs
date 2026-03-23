namespace Serilog.Sinks.ApplicationInsights.AspNetCore.UnitTests;

[TestFixture]
public sealed class TelemetryInitializersTests
{
    [Test]
    public void W3cActivityTelemetryInitializer_throws_when_options_null()
    {
        Assert.Throws<ArgumentNullException>(() => new W3cActivityTelemetryInitializer(null!));
    }

    [Test]
    public void ExtensionVersionTelemetryInitializer_throws_when_options_null()
    {
        Assert.Throws<ArgumentNullException>(() => new ExtensionVersionTelemetryInitializer(null!));
    }

    [Test]
    public void W3cActivityTelemetryInitializer_skips_when_option_disabled()
    {
        var opts = Options.Create(new CrossErrorHandlersApplicationInsightsOptions { EnrichOperationFromActivity = false });
        var init = new W3cActivityTelemetryInitializer(opts);
        var tel = new EventTelemetry("e");
        tel.Context.Operation.Id = string.Empty;
        tel.Context.Operation.ParentId = string.Empty;
        init.Initialize(tel);
        tel.Context.Operation.Id.Should().BeNullOrEmpty();
    }

    [Test]
    public void W3cActivityTelemetryInitializer_skips_when_no_activity()
    {
        var opts = Options.Create(new CrossErrorHandlersApplicationInsightsOptions { EnrichOperationFromActivity = true });
        var init = new W3cActivityTelemetryInitializer(opts);
        var tel = new EventTelemetry("e");
        using (new ActivityCurrentReplacer(null))
        {
            init.Initialize(tel);
        }
        tel.Context.Operation.Id.Should().BeNullOrEmpty();
    }

    [Test]
    public void W3cActivityTelemetryInitializer_sets_operation_from_current_activity()
    {
        using (new ActivityCurrentReplacer(null))
        {
            var opts = Options.Create(new CrossErrorHandlersApplicationInsightsOptions { EnrichOperationFromActivity = true });
            var init = new W3cActivityTelemetryInitializer(opts);
            using var activity = new Activity("Test");
            activity.SetIdFormat(ActivityIdFormat.W3C);
            activity.Start();
            var tel = new EventTelemetry("e");
            init.Initialize(tel);
            tel.Context.Operation.Id.Should().Be(activity.TraceId.ToHexString());
            tel.Context.Operation.ParentId.Should().Be(activity.SpanId.ToHexString());
        }
    }

    [Test]
    public void W3cActivityTelemetryInitializer_adds_trace_state_to_properties()
    {
        using (new ActivityCurrentReplacer(null))
        {
            var opts = Options.Create(new CrossErrorHandlersApplicationInsightsOptions { EnrichOperationFromActivity = true });
            var init = new W3cActivityTelemetryInitializer(opts);
            using var activity = new Activity("Test");
            activity.SetIdFormat(ActivityIdFormat.W3C);
            activity.TraceStateString = "k=v";
            activity.Start();
            var tel = new EventTelemetry("e");
            init.Initialize(tel);
            tel.Properties.Should().ContainKey("w3cTraceState");
            tel.Properties["w3cTraceState"].Should().Be("k=v");
        }
    }

    [Test]
    public void ExtensionVersionTelemetryInitializer_skips_when_disabled()
    {
        var opts = Options.Create(new CrossErrorHandlersApplicationInsightsOptions { AddExtensionVersionProperty = false });
        var init = new ExtensionVersionTelemetryInitializer(opts);
        var tel = new EventTelemetry("e");
        init.Initialize(tel);
        tel.Properties.Should().NotContainKey("Serilog.Sinks.ApplicationInsights.AspNetCore.Version");
    }

    [Test]
    public void ExtensionVersionTelemetryInitializer_adds_version_once()
    {
        var opts = Options.Create(new CrossErrorHandlersApplicationInsightsOptions { AddExtensionVersionProperty = true });
        var init = new ExtensionVersionTelemetryInitializer(opts);
        var tel = new EventTelemetry("e");
        init.Initialize(tel);
        init.Initialize(tel);
        tel.Properties.Should().ContainKey("Serilog.Sinks.ApplicationInsights.AspNetCore.Version");
        tel.Properties["Serilog.Sinks.ApplicationInsights.AspNetCore.Version"].Should().NotBeNullOrEmpty();
    }

    [Test]
    public void ExtensionVersionTelemetryInitializer_skips_when_telemetry_does_not_support_properties()
    {
        var opts = Options.Create(new CrossErrorHandlersApplicationInsightsOptions { AddExtensionVersionProperty = true });
        var init = new ExtensionVersionTelemetryInitializer(opts);
        var tel = Mock.Of<ITelemetry>();
        var act = () => init.Initialize(tel);
        act.Should().NotThrow();
    }

}
