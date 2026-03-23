namespace Serilog.Sinks.ApplicationInsights.AspNetCore;

/// <summary>
/// Adds a stable custom dimension so you can tell which apps run this extension and which version.
/// </summary>
internal sealed class ExtensionVersionTelemetryInitializer : ITelemetryInitializer
{
    private readonly IOptions<CrossErrorHandlersApplicationInsightsOptions> _options;
    private readonly string _version;

    public ExtensionVersionTelemetryInitializer(IOptions<CrossErrorHandlersApplicationInsightsOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _version = typeof(ExtensionVersionTelemetryInitializer).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(ExtensionVersionTelemetryInitializer).Assembly.GetName().Version?.ToString()
            ?? "unknown";
    }

    public void Initialize(ITelemetry telemetry)
    {
        if (!_options.Value.AddExtensionVersionProperty)
            return;

        if (telemetry is not ISupportProperties supportProperties)
            return;

        supportProperties.Properties.TryAdd("Serilog.Sinks.ApplicationInsights.AspNetCore.Version", _version);
    }
}
