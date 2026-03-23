namespace Serilog.Sinks.ApplicationInsights.AspNetCore;

/// <summary>
/// Options for <see cref="CrossErrorHandlersApplicationInsightsServiceCollectionExtensions.AddCrossErrorHandlersApplicationInsights"/>.
/// </summary>
public sealed class CrossErrorHandlersApplicationInsightsOptions
{
    /// <summary>
    /// When true, copies W3C trace/span identifiers from <see cref="Activity.Current"/> onto telemetry
    /// when Application Insights has not already set operation id/parent id (helps correlate ILogger traces with requests).
    /// </summary>
    public bool EnrichOperationFromActivity { get; set; } = true;

    /// <summary>
    /// When true, adds a custom dimension with the version of this extension assembly (useful when filtering in Azure).
    /// </summary>
    public bool AddExtensionVersionProperty { get; set; } = true;
}
