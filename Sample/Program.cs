var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Services
    .AddSerilogApplicationInsights(builder.Configuration, enableRequestTrackingTelemetry: false)
    .AddApplicationInsightsAzureAD(builder.Configuration)
    .AddCrossErrorHandlersApplicationInsights();

var app = builder.Build();

app.UseTelemetryBaseInitializer();

app.MapGet("/", () => "Serilog.Sinks.ApplicationInsights.AspNetCore demo — set ApplicationInsights:ConnectionString or APPLICATIONINSIGHTS_CONNECTION_STRING to send telemetry.");

app.Run();
