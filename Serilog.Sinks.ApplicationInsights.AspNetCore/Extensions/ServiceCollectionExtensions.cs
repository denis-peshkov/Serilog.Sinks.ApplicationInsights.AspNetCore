namespace Serilog.Sinks.ApplicationInsights.AspNetCore.Extensions;

/// <summary>
/// Registers Application Insights telemetry initializers: W3C activity correlation for logger-based telemetry and an optional assembly version dimension.
/// Call after <c>AddApplicationInsightsTelemetry</c>. App-specific <see cref="ITelemetryInitializer"/> implementations can be registered the same way.
/// </summary>
public static class ServiceCollectionExtensions
{

    public static IServiceCollection AddSerilogApplicationInsights(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableRequestTrackingTelemetry = false)
    {
        // Enables request/dependency telemetry and hooks up ILogger → Application Insights
        // Application Insights: automatic request/dependency tracking and ILogger integration. Config: ApplicationInsights section and APPLICATIONINSIGHTS_CONNECTION_STRING.
        var aiOptions = new ApplicationInsightsServiceOptions();
        aiOptions.EnableRequestTrackingTelemetryModule = enableRequestTrackingTelemetry;  // disable automatic HTTP request tracking
        configuration.GetSection("ApplicationInsights").Bind(aiOptions);
        services.AddApplicationInsightsTelemetry(aiOptions);

        return services;
    }

    public static IServiceCollection AddApplicationInsightsAzureAD(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Optional: use Azure AD (e.g. DefaultAzureCredential) for App Insights ingestion when connection uses AAD auth (e.g. managed identity in Azure).
        if (configuration.GetValue<bool>("ApplicationInsights:UseAzureCredential"))
        {
            services.Configure<TelemetryConfiguration>(config =>
            {
                var credential = new Azure.Identity.DefaultAzureCredential();
                config.SetAzureTokenCredential(credential);
            });
        }

        return services;
    }

    /// <summary>
    /// Adds telemetry initializers for Application Insights.
    /// </summary>
    public static IServiceCollection AddCrossErrorHandlersApplicationInsights(
        this IServiceCollection services,
        Action<CrossErrorHandlersApplicationInsightsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<CrossErrorHandlersApplicationInsightsOptions>(o => configure?.Invoke(o));

        services.AddSingleton<ITelemetryInitializer, W3cActivityTelemetryInitializer>();
        services.AddSingleton<ITelemetryInitializer, ExtensionVersionTelemetryInitializer>();

        return services;
    }

}
