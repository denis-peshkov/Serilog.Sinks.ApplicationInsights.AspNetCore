namespace Serilog.Sinks.ApplicationInsights.AspNetCore.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Sets Application Insights global dimensions for all telemetry (including from ILogger/Serilog)
    /// from <c>Serilog:Properties:Application</c>, <c>Module</c>, and <c>Env</c>.
    /// </summary>
    public static WebApplication UseTelemetryBaseInitializer(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var telemetryClient = app.Services.GetRequiredService<TelemetryClient>();
        var config = app.Configuration;
        if (config["Serilog:Properties:Application"] is { } appName)
            telemetryClient.Context.GlobalProperties["Application"] = appName;
        if (config["Serilog:Properties:Module"] is { } module)
            telemetryClient.Context.GlobalProperties["Module"] = module;
        if (config["Serilog:Properties:Env"] is { } env)
            telemetryClient.Context.GlobalProperties["Env"] = env;

        return app;
    }
}
