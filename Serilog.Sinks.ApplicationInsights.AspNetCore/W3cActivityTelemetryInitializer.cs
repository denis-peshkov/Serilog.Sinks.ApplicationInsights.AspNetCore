namespace Serilog.Sinks.ApplicationInsights.AspNetCore;

/// <summary>
/// Aligns telemetry operation id/parent with the current <see cref="Activity"/> when the SDK left them empty,
/// so logs and custom telemetry stay on the same distributed trace as the incoming request.
/// </summary>
internal sealed class W3cActivityTelemetryInitializer : ITelemetryInitializer
{
    private readonly IOptions<CrossErrorHandlersApplicationInsightsOptions> _options;

    public W3cActivityTelemetryInitializer(IOptions<CrossErrorHandlersApplicationInsightsOptions> options) =>
        _options = options ?? throw new ArgumentNullException(nameof(options));

    public void Initialize(ITelemetry telemetry)
    {
        if (!_options.Value.EnrichOperationFromActivity)
            return;

        var activity = Activity.Current;
        if (activity is null)
            return;

        if (string.IsNullOrEmpty(telemetry.Context.Operation.Id))
            telemetry.Context.Operation.Id = activity.TraceId.ToHexString();

        if (string.IsNullOrEmpty(telemetry.Context.Operation.ParentId) && activity.SpanId != default)
            telemetry.Context.Operation.ParentId = activity.SpanId.ToHexString();

        if (telemetry is ISupportProperties support && activity.TraceStateString is { Length: > 0 } traceState)
            support.Properties.TryAdd("w3cTraceState", traceState);
    }
}
