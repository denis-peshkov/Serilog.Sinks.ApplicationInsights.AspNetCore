[![License](https://img.shields.io/github/license/denis-peshkov/Serilog.Sinks.ApplicationInsights.AspNetCore)](LICENSE)
[![GitHub Release Date](https://img.shields.io/github/release-date/denis-peshkov/Serilog.Sinks.ApplicationInsights.AspNetCore?label=released)](https://github.com/denis-peshkov/Serilog.Sinks.ApplicationInsights.AspNetCore/releases)
[![NuGetVersion](https://img.shields.io/nuget/v/Serilog.Sinks.ApplicationInsights.AspNetCore.svg)](https://nuget.org/packages/Serilog.Sinks.ApplicationInsights.AspNetCore/)
[![NugetDownloads](https://img.shields.io/nuget/dt/Serilog.Sinks.ApplicationInsights.AspNetCore.svg)](https://nuget.org/packages/Serilog.Sinks.ApplicationInsights.AspNetCore/)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=Serilog.Sinks.ApplicationInsights.AspNetCore&metric=coverage)](https://sonarcloud.io/summary/new_code?id=Serilog.Sinks.ApplicationInsights.AspNetCore)
[![issues](https://img.shields.io/github/issues/denis-peshkov/Serilog.Sinks.ApplicationInsights.AspNetCore)](https://github.com/denis-peshkov/Serilog.Sinks.ApplicationInsights.AspNetCore/issues)
[![.NET PR](https://github.com/denis-peshkov/Serilog.Sinks.ApplicationInsights.AspNetCore/actions/workflows/dotnet.yml/badge.svg?event=pull_request)](https://github.com/denis-peshkov/Serilog.Sinks.ApplicationInsights.AspNetCore/actions/workflows/dotnet.yml)

![Size](https://img.shields.io/github/repo-size/denis-peshkov/Serilog.Sinks.ApplicationInsights.AspNetCore)
[![GitHub contributors](https://img.shields.io/github/contributors/denis-peshkov/Serilog.Sinks.ApplicationInsights.AspNetCore)](https://github.com/denis-peshkov/Serilog.Sinks.ApplicationInsights.AspNetCore/contributors)
[![GitHub commits since latest release (by date)](https://img.shields.io/github/commits-since/denis-peshkov/Serilog.Sinks.ApplicationInsights.AspNetCore/latest?label=new+commits)](https://github.com/denis-peshkov/Serilog.Sinks.ApplicationInsights.AspNetCore/commits/master)
![Activity](https://img.shields.io/github/commit-activity/w/denis-peshkov/Serilog.Sinks.ApplicationInsights.AspNetCore)
![Activity](https://img.shields.io/github/commit-activity/m/denis-peshkov/Serilog.Sinks.ApplicationInsights.AspNetCore)
![Activity](https://img.shields.io/github/commit-activity/y/denis-peshkov/Serilog.Sinks.ApplicationInsights.AspNetCore)

# Serilog.Sinks.ApplicationInsights.AspNetCore

Companion helpers for **[Serilog](https://serilog.net/)** and **[Application Insights](https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview)** on **ASP.NET Core**: DI registration, W3C correlation initializers, optional request logging middleware, and **ILogger** extensions that emit structured telemetry when used with **Serilog.Sinks.ApplicationInsights** and the included converters.

**Target frameworks:** `net6.0`, `net7.0`, `net8.0`, `net9.0`, `net10.0`

## Features

- **`AddSerilogApplicationInsights`** — registers Application Insights for ASP.NET Core (`AddApplicationInsightsTelemetry`) and binds the `ApplicationInsights` configuration section.
- **`AddApplicationInsightsAzureAD`** — optional **DefaultAzureCredential** for ingestion when `ApplicationInsights:UseAzureCredential` is true.
- **`AddCrossErrorHandlersApplicationInsights`** — registers **`W3cActivityTelemetryInitializer`** and **`ExtensionVersionTelemetryInitializer`**.
- **`UseTelemetryBaseInitializer`** — copies `Serilog:Properties:Application`, `Module`, and `Env` into **TelemetryClient.Context.GlobalProperties**.
- **`ApplicationInsightsRequestLoggingMiddleware`** — emits **Request**-style telemetry per HTTP call via **`LogAppInsightsRequest`** (use with Serilog AI sink + **`ApplicationInsightsTelemetryConverter`**).
- **`LoggerApplicationInsightsExtensions`** — **`LogAppInsightsEvent`**, **`Metric`**, **`Dependency`**, **`Request`**, **`Exception`** on **`ILogger`**.

## Install NuGet package

Install the _Serilog.Sinks.ApplicationInsights.AspNetCore_ [NuGet package](https://www.nuget.org/packages/Serilog.Sinks.ApplicationInsights.AspNetCore/) into your .NET project:

```powershell
Install-Package Serilog.Sinks.ApplicationInsights.AspNetCore
```
or
```bash
dotnet add package Serilog.Sinks.ApplicationInsights.AspNetCore
```

## Quick start (minimal API)

```csharp
using Serilog;
using Serilog.Sinks.ApplicationInsights.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Services
    .AddSerilogApplicationInsights(builder.Configuration)
    .AddApplicationInsightsAzureAD(builder.Configuration)
    .AddCrossErrorHandlersApplicationInsights();

var app = builder.Build();
app.UseTelemetryBaseInitializer();
app.MapGet("/", () => "OK");
app.Run();
```

Set **`ApplicationInsights:ConnectionString`** or the **`APPLICATIONINSIGHTS_CONNECTION_STRING`** environment variable for live ingestion.

## Demo

```bash
dotnet run --project Serilog.Sinks.ApplicationInsights.AspNetCore.Demo/Serilog.Sinks.ApplicationInsights.AspNetCore.Demo.csproj
```

Then open the URL from `Properties/launchSettings.json` (default **http://localhost:5088**).

## Documentation

See **[Logging and Metrics](LOGGING-AND-METRICS.md)** for configuration, **`ILogger`** telemetry extensions, converters, optional request middleware, **`TelemetryClient.StartOperation`**, property keys, limits, and using the Azure portal.

## Release notes

See [ReleaseNotes.md](ReleaseNotes.md).

## Contributing

1. Fork the repository.
2. Create a feature branch (`git checkout -b feature/your-change`).
3. Commit your changes with a clear message.
4. Push the branch and open a pull request.

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
