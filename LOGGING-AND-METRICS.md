# Logging and Metrics Guide

This document describes how logging and Application Insights telemetry work when an **ASP.NET Core** app uses **Serilog**, the **Application Insights** SDK, and the **[Serilog.Sinks.ApplicationInsights.AspNetCore](https://www.nuget.org/packages/Serilog.Sinks.ApplicationInsights.AspNetCore/)** package (DI helpers, **ILogger** extensions, converters, and optional middleware). The sample host **Sample** is minimal; larger apps add their own enrichers, `appsettings`, and optional **`ITelemetryInitializer`** types—map those ideas to your project.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Configuration](#2-configuration)
   - [Optional: ApplicationInsightsRequestLoggingMiddleware](#optional-applicationinsightsrequestloggingmiddleware)
3. [Standard Logging (ILogger)](#3-standard-logging-ilogger)
4. [Serilog and Application Insights](#4-serilog-and-application-insights)
   - [4.1 All telemetry converters](#41-all-telemetry-converters)
5. [Custom Application Insights Telemetry via ILogger](#5-custom-application-insights-telemetry-via-ilogger)
6. [Custom Operations with TelemetryClient](#6-custom-operations-with-telemetryclient)
   - [When to use StartOperation<RequestTelemetry>](#when-to-use-startoperationrequesttelemetry)
   - [How to consolidate metrics, events, and logs into a single RequestTelemetry](#how-to-consolidate-metrics-events-and-logs-into-a-single-requesttelemetry)
7. [Property Keys and Limits](#7-property-keys-and-limits)
   - [7.1 All TelemetryType values](#71-all-telemetrytype-values)
8. [Viewing Data in Azure Portal](#8-viewing-data-in-azure-portal)
9. [Best Practices](#9-best-practices)
10. [References](#references)

---

## 1. Overview

The application uses:

| Layer | Purpose |
|-------|--------|
| **ILogger** | Your code logs via `ILogger<T>` (injected). |
| **Serilog** | Primary logging pipeline; captures ILogger output, enriches with context (CorrelationId, TaxYear, etc.), and writes to sinks. |
| **Serilog sinks** | **Application Insights** (with a custom converter) and **File** (and **Console** in Development). |
| **Application Insights SDK** | Registered via **`AddSerilogApplicationInsights`** ( **`Serilog.Sinks.ApplicationInsights.AspNetCore`** library): binds **`ApplicationInsights`** options, keeps **dependency** and **ILogger → AI** integration, and turns **off** the **automatic HTTP request tracking module** by default (no automatic **Request** row per inbound HTTP call unless you opt in). |
| **Custom converter** | `ApplicationInsightsTelemetryConverter`: turns Serilog events with a `TelemetryType` property into Event, Metric, Dependency, Exception, Request, or Trace telemetry. |

So:

- **Normal logs** (e.g. `_logger.LogInformation("...")`) go to Serilog → File/Console and, when the AI sink is used, to Application Insights as **traces** (or **exceptions** if an exception is attached).
- **HTTP request rows** in the **requests** table are **not** auto-created for every call by the SDK in the default setup; use **`LogAppInsightsRequest`**, **`ApplicationInsightsRequestLoggingMiddleware`**, **`TelemetryClient.StartOperation<RequestTelemetry>`**, or pass **`enableRequestTrackingTelemetry: true`** into **`AddSerilogApplicationInsights`** if you want per-request-style **Request** telemetry.
- **Custom telemetry** (events, metrics, dependencies, requests, exceptions) is sent by using the **ILogger extensions** in **`Serilog.Sinks.ApplicationInsights.AspNetCore.Core.LoggerApplicationInsightsExtensions`** (or by injecting `TelemetryClient` and calling the SDK directly). The extensions put a `TelemetryType` and type-specific properties in the log scope; the converter then produces the corresponding Application Insights telemetry.

---

## 2. Configuration

### 2.1 Application Insights

Configuration is under the **`ApplicationInsights`** section in `appsettings.json` (and environment-specific files).

| Setting | Description | Default / Notes |
|--------|--------------|------------------|
| **ConnectionString** | Application Insights resource connection string. | Required. Override in production via `APPLICATIONINSIGHTS_CONNECTION_STRING` env var. |
| **EnableAdaptiveSampling** | When true, SDK reduces telemetry volume under load while keeping full request traces. | `false` in this app. Set `true` to limit cost at high load. |
| **EnableQuickPulseMetricStream** | Enables Live Metrics in the Azure portal (near real-time). | `true`. Set `false` to disable. |
| **UseAzureCredential** | When true, uses Azure AD (e.g. `DefaultAzureCredential`) for App Insights ingestion. | `false`. Set `true` when the resource uses AAD auth (e.g. managed identity). |

Example in `appsettings.json`:

```json
"ApplicationInsights": {
  "ConnectionString": "InstrumentationKey=...;IngestionEndpoint=...;LiveEndpoint=...;ApplicationId=...",
  "EnableAdaptiveSampling": false,
  "EnableQuickPulseMetricStream": true,
  "UseAzureCredential": false
}
```

The Serilog Application Insights sink also needs the connection string in **`Serilog:WriteTo`** (see `appsettings.json`). Keep it in sync with `ApplicationInsights:ConnectionString` or the same env var in production.

### 2.2 Logging levels

- **Logging:LogLevel** – Default minimum level for all loggers.
- **Logging:ApplicationInsights:LogLevel** – Level for the built-in Application Insights logger (e.g. `Information` so ILogger → App Insights sends Information and above).
- **Serilog:MinimumLevel** – Serilog’s minimum level and overrides (e.g. `Microsoft: Warning`).

### 2.3 Serilog

- **Enrichers** add properties to every log event (e.g. `CorrelationId` from `X-Correlation-Id`, `TaxYear` from `X-Tax-Year`, process ID, client IP).
- **WriteTo** defines sinks: Application Insights (with `ApplicationInsightsTelemetryConverter`) and File (and Console in Development).
- **Serilog:Properties** – **Application**, **Module**, and **Env** are used as Serilog global properties and are also applied to **`TelemetryClient.Context.GlobalProperties`** at startup via **`UseTelemetryBaseInitializer()`** (see §2.4).

### 2.4 Application Insights registration and global properties

In **`Startup.ConfigureServices`** the API typically chains (from **`Serilog.Sinks.ApplicationInsights.AspNetCore`**):

| Call | Role |
|------|------|
| **`AddSerilogApplicationInsights(configuration)`** | **`AddApplicationInsightsTelemetry`** with options bound from **`ApplicationInsights`** (§2.1). **`EnableRequestTrackingTelemetryModule`** defaults to **`false`** unless you pass **`enableRequestTrackingTelemetry: true`**. |
| **`AddApplicationInsightsAzureAD(configuration)`** | When **`ApplicationInsights:UseAzureCredential`** is **`true`**, sets **`DefaultAzureCredential`** on **`TelemetryConfiguration`** for ingestion. |
| **`AddCrossErrorHandlersApplicationInsights()`** | Registers **`ITelemetryInitializer`** types: W3C **Activity** → operation id/parent when empty; optional dimension **`Serilog.Sinks.ApplicationInsights.AspNetCore.Version`**. Configure with **`CrossErrorHandlersApplicationInsightsOptions`**. |

Some host solutions also register a custom **`RequestContextTelemetryInitializer`** (see §2.5); that type is **not** included in the **Serilog.Sinks.ApplicationInsights.AspNetCore** package.

In **`Startup.ConfigureApp`**, call **`app.UseTelemetryBaseInitializer()`** early (before middleware that emits telemetry). It reads **`Serilog:Properties:Application`**, **`Module`**, and **`Env`** and sets **`TelemetryClient.Context.GlobalProperties`** so SDK-sourced telemetry is tagged consistently.

### Optional: `ApplicationInsightsRequestLoggingMiddleware`

The **Serilog.Sinks.ApplicationInsights.AspNetCore** assembly ships **`ApplicationInsightsRequestLoggingMiddleware`**. If the SDK **automatic HTTP request tracking module** is **off** (default for **`AddSerilogApplicationInsights`**), you can still emit one **Request**-shaped telemetry item per inbound HTTP call (through **`LogAppInsightsRequest`**) and a **duration metric** keyed by route by adding this middleware to the pipeline—**before** exception-handling middleware so **`await _next`** completes after the status code is finalized.

- **`success`** is **`true`** when `HttpResponse.StatusCode < 400`; **4xx** and **5xx** are **`false`**.
- Logging runs in a **`finally`** block, so telemetry is still written when the downstream pipeline throws after setting the response code.
- Paths skipped by **`UrlHelper.IsPathExcludedFromValidation`**: empty path, **`/`**, anything starting with **`/swagger`** (case-insensitive), and **`/health`** (case-insensitive).

Configure **Serilog.Sinks.ApplicationInsights** with **`ApplicationInsightsTelemetryConverter`** so scope properties become **RequestTelemetry** (and metrics) in Application Insights.

### 2.5 Request telemetry and custom dimensions (requests table)

With **automatic HTTP request tracking disabled** (default), the **requests** table is **not** automatically filled with one **RequestTelemetry** per inbound HTTP call. Rows still appear when you emit **Request** telemetry explicitly (e.g. **`LogAppInsightsRequest`**, **`StartOperation<RequestTelemetry>`**) or if you enable the request tracking module.

**`ITelemetryInitializer`** runs for **each** telemetry item the SDK sends. In a larger solution you might register an additional initializer (not part of the **Serilog.Sinks.ApplicationInsights.AspNetCore** package) to copy **CorrelationId**, **TaxYear**, **RequestPath**, **ClientIp**, **Application**, **Module**, and **Env** onto items when an HTTP request is active (so **customDimensions** match Serilog-enriched traces). For example:

- **`RequestContextTelemetryInitializer`** (not part of this package; you might add it next to **Sample** or your API) – When **`HttpContext`** is present, reads **`IHeaderContextAccessor`** (CorrelationId, TaxYear), **RequestPath**, **ClientIp**, and merges **Application** / **Module** / **Env** from configuration into **`telemetry.Properties`** (in addition to **`UseTelemetryBaseInitializer`** global properties on the client).

---

## 3. Standard Logging (ILogger)

### 3.1 Injecting the logger

In any class resolved from DI (controllers, handlers, services), inject `ILogger<T>`:

```csharp
public class MyController : ControllerBase
{
    private readonly ILogger<MyController> _logger;

    public MyController(ILogger<MyController> logger)
    {
        _logger = logger;
    }
}
```

### 3.2 Log levels and messages

Use the appropriate level so Serilog and Application Insights can filter correctly:

```csharp
_logger.LogTrace("Very detailed diagnostic.");
_logger.LogDebug("Debug info: {Value}", someValue);
_logger.LogInformation("Request processed for {UserId}.", userId);
_logger.LogWarning("Retry attempt {Attempt} for {Id}.", attempt, id);
_logger.LogError(exception, "Failed to process {Id}.", id);
_logger.LogCritical("Critical failure: {Message}.", message);
```

Use structured properties (the `{Name}` placeholders) so you can query and filter in Application Insights and in log files.

### 3.3 Scopes

Add scope data so all logs inside a block share the same properties (and appear under the same operation in App Insights when correlation is enabled):

```csharp
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["OrderId"] = orderId,
    ["UserId"] = userId
}))
{
    _logger.LogInformation("Processing order.");
    // ... all logs here will include OrderId and UserId
}
```

### 3.4 What happens to these logs

- They go through Serilog (with enrichers) and are written to the configured sinks (File, Console, Application Insights).
- When sent to Application Insights via the Serilog sink, they become **Trace** telemetry (or **Exception** if `LogError(ex, ...)` is used and the converter handles it). They are correlated with the current HTTP request when in a request context. In Log Analytics they appear in the **traces** table (see [TrackTrace](https://learn.microsoft.com/en-us/azure/azure-monitor/app/classic-api?tabs=dotnet#tracktrace)).

---

## 4. Serilog and Application Insights

- The app uses **Serilog** as the main logging pipeline and **Serilog.Sinks.ApplicationInsights** to send log events to Application Insights.
- The sink uses **`ApplicationInsightsTelemetryConverter`**, which:
  - Reads a **`TelemetryType`** property from the log event (set by the ILogger extensions or by custom scope).
  - Produces **Event**, **Metric**, **Dependency**, **Exception**, **Request**, or **Trace** telemetry accordingly.
  - If `TelemetryType` is missing or unknown, the event is sent as **Trace** telemetry.
- So: “normal” logs become traces; the **custom telemetry** (events, metrics, dependencies, requests, exceptions) is produced when you use the extensions (or equivalent scope) described in section 5.

### 4.1 All telemetry converters

The project defines **two** Serilog → Application Insights converters (both implement `Serilog.Sinks.ApplicationInsights.TelemetryConverters.ITelemetryConverter`). Only one is used at a time, via the `telemetryConverter` argument in **Serilog:WriteTo** (Application Insights sink).

| Converter | Class / assembly | Behavior | When to use |
|-----------|-------------------|----------|-------------|
| **ApplicationInsightsTelemetryConverter** | `Serilog.Sinks.ApplicationInsights.AspNetCore.Core.ApplicationInsightsTelemetryConverter, Serilog.Sinks.ApplicationInsights.AspNetCore` | Reads **`TelemetryType`** from the log event. Emits **Event**, **Metric**, **Dependency**, **Exception**, **Request**, or **Trace** as appropriate. If `TelemetryType` is missing, emits **Trace**. Enforces message/property length limits (32K / 8K). Copies non-reserved properties to custom dimensions; for events, properties with key prefix **`Metric_`** are sent as **EventTelemetry.Metrics**. | **Recommended default** when using the ILogger extensions (section 5). |
| **MinimalTraceTelemetryConverter** | `Serilog.Sinks.ApplicationInsights.AspNetCore.Core.MinimalTraceTelemetryConverter, Serilog.Sinks.ApplicationInsights.AspNetCore` | Converts **every** log event to **TraceTelemetry** only. Maps Serilog level to `SeverityLevel`; appends exception to message if present. Does **not** use `TelemetryConverterBase.PopulateTelemetryFromLogEvent` (avoids `OperationContext` and potential `MethodAccessException` with some App Insights SDK versions). No custom dimensions from log properties. | Use only if you need trace-only ingestion and the full converter causes issues (e.g. with OperationContext). Custom events/metrics/dependencies/requests will **not** be emitted. |

**Currently configured:** `appsettings.json` and `appsettings.Development.json` set `telemetryConverter` to **ApplicationInsightsTelemetryConverter**. To switch to the minimal converter (trace-only), change the sink config to:

```json
"telemetryConverter": "Serilog.Sinks.ApplicationInsights.AspNetCore.Core.MinimalTraceTelemetryConverter, Serilog.Sinks.ApplicationInsights.AspNetCore"
```

---

## 5. Custom Application Insights Telemetry via ILogger

To send **custom events**, **metrics**, **dependencies**, **requests**, or **exception** telemetry, use the extension methods in **`LoggerApplicationInsightsExtensions`** (**namespace** **`Serilog.Sinks.ApplicationInsights.AspNetCore.Core`**). They work by setting a log scope with `TelemetryType` and type-specific properties; the Serilog sink and converter then emit the correct Application Insights type.

**Namespace:** `Serilog.Sinks.ApplicationInsights.AspNetCore.Core`
**Class:** `LoggerApplicationInsightsExtensions`

Inject **`ILogger<T>`** and call the extensions. No need to reference `TelemetryClient` for these.

---

### 5.1 Custom events (EventTelemetry)

Use when something notable happens (e.g. “OrderSubmitted”, “DocumentGenerated”). You can attach **properties** (string dimensions) and **metrics** (numeric values for charts).

**Method:** `LogAppInsightsEvent`

```csharp
using Serilog.Sinks.ApplicationInsights.AspNetCore.Core;

// Simple event
_logger.LogAppInsightsEvent("OrderSubmitted");

// Event with properties (custom dimensions)
_logger.LogAppInsightsEvent(
    "DocumentGenerated",
    properties: new Dictionary<string, object?>
    {
        ["DocumentType"] = "1099",
        ["FidId"] = fidId.ToString()
    });

// Event with numeric metrics (for charts/aggregation in Application Insights)
_logger.LogAppInsightsEvent(
    "BatchProcessed",
    properties: new Dictionary<string, object?> { ["BatchId"] = batchId },
    metrics: new Dictionary<string, double>
    {
        ["ItemCount"] = 42,
        ["DurationMs"] = 1250.5
    });
```

In Application Insights these appear as **Custom Events** with optional **custom dimensions** and **custom measurements**.

---

### 5.2 Metrics (MetricTelemetry)

Use for numeric values you want to track over time (counters, durations, sizes). Each call sends one metric data point.

**Method:** `LogAppInsightsMetric`

```csharp
_logger.LogAppInsightsMetric("OrdersPerMinute", 15.5);

_logger.LogAppInsightsMetric(
    "QueueLength",
    value: queueLength,
    properties: new Dictionary<string, object?> { ["QueueName"] = "orders" });
```

Use **events with metrics** (section 5.1) when you want several metrics attached to one logical event.

---

### 5.3 Dependencies (DependencyTelemetry)

Use for outbound calls: HTTP, SQL, external APIs, etc. This helps with performance and failure analysis in Application Insights.

**Method:** `LogAppInsightsDependency`

```csharp
var sw = Stopwatch.StartNew();
bool success = true;
try
{
    var response = await _httpClient.GetAsync(externalUrl);
    success = response.IsSuccessStatusCode;
    // ...
}
finally
{
    sw.Stop();
    _logger.LogAppInsightsDependency(
        dependencyTypeName: "HTTP",
        target: new Uri(externalUrl).Host,
        name: "GET " + externalUrl,
        durationMs: sw.ElapsedMilliseconds,
        success: success,
        data: externalUrl,
        resultCode: response?.StatusCode.ToString(),
        properties: new Dictionary<string, object?> { ["Endpoint"] = "TaxService" });
}
```

Optional parameters:

- **data** – Command or text (e.g. URL path, SQL text).
- **resultCode** – Status or result code (e.g. HTTP status).
- **properties** – Extra dimensions.

---

### 5.4 Requests (RequestTelemetry)

Use for logical “requests” that are not HTTP (e.g. background job, worker iteration). Each call produces one request in Application Insights with duration and success.

**Method:** `LogAppInsightsRequest`

```csharp
var sw = Stopwatch.StartNew();
bool success = true;
string responseCode = "200";
try
{
    await ProcessJobAsync(correlationId);
}
catch (Exception ex)
{
    success = false;
    responseCode = "500";
    _logger.LogError(ex, "Job failed.");
}
finally
{
    sw.Stop();
    _logger.LogAppInsightsRequest(
        requestName: "ProcessJob",
        durationMs: sw.ElapsedMilliseconds,
        responseCode: responseCode,
        success: success,
        url: $"https://api.example.com/jobs/{correlationId}",
        requestId: correlationId.ToString(),
        properties: new Dictionary<string, object?> { ["JobType"] = "TaxRun" });
}
```

---

### 5.5 Exceptions (ExceptionTelemetry)

Use when you catch an exception and want it reported to Application Insights (with or without rethrowing). You can send an exception with or without an `Exception` object (e.g. from another system).

**Method:** `LogAppInsightsException`

```csharp
try
{
    await ExternalCallAsync();
}
catch (Exception ex)
{
    _logger.LogAppInsightsException(
        ex,
        message: "External call failed.",
        properties: new Dictionary<string, object?> { ["Endpoint"] = "TaxService" });
    throw;
}

// Without an exception object (e.g. error from another service):
_logger.LogAppInsightsException(
    exception: null,
    message: "Validation failed: invalid tax year.",
    properties: new Dictionary<string, object?> { ["Source"] = "Validator" });
```

---

## 6. Custom Operations with TelemetryClient

### When to use `StartOperation<RequestTelemetry>`

Use **`StartOperation<RequestTelemetry>`** when you need **one logical “request” in Application Insights** that wraps a whole unit of work and you want **all telemetry inside it correlated** under a single operation. Typical cases:

| Scenario | Use StartOperation? | Alternative |
|----------|---------------------|-------------|
| **Background job / worker iteration** (e.g. process one message, run scheduled task) | **Yes** – one request per job with duration, success, and all logs/dependencies under it | — |
| **Multi-step workflow** you want to see as one transaction in the portal | **Yes** – one request for the whole workflow | — |
| **HTTP request** (MVC/API) | **Sometimes** – SDK module if **`enableRequestTrackingTelemetry: true`**, or **`ApplicationInsightsRequestLoggingMiddleware`**, or manual **`LogAppInsightsRequest` / `StartOperation`** | With **default** **`AddSerilogApplicationInsights`**, per-HTTP **Request** rows are **not** automatic; see §2 overview and optional middleware above. |
| **Single event or metric** (e.g. “OrderSubmitted”, “ItemsProcessed: 42”) | No | `LogAppInsightsEvent` / `LogAppInsightsMetric` |
| **Single dependency** (one outbound call) | No | `LogAppInsightsDependency` |
| **Single logical request** (name + duration + success, no need for full correlation) | Optional | `LogAppInsightsRequest` |

In short: use **StartOperation** when you need **one request + full correlation** for everything that happens inside the block. Use the **ILogger extensions** when you only need individual events, metrics, dependencies, or requests.

### How to consolidate metrics, events, and logs into a single RequestTelemetry

To have **one RequestTelemetry** in Application Insights that groups **all** telemetry for a unit of work (logs, events, metrics, dependencies, exceptions):

1. **Inject `TelemetryClient`** in the class that runs the operation (e.g. background job handler).
2. **Wrap the unit of work** in `using var operation = _telemetryClient.StartOperation<RequestTelemetry>("YourOperationName");`.
3. **Do all work inside that `using` block** and use normal logging and the ILogger extensions there:
   - **Logs** – `_logger.LogInformation(...)`, `_logger.LogWarning(...)` etc. → become **traces** under this request.
   - **Events** – `_logger.LogAppInsightsEvent("StepCompleted", ...)` → **custom events** under this request.
   - **Metrics** – `_logger.LogAppInsightsMetric("ItemsProcessed", count)` → **custom metrics** under this request.
   - **Dependencies** – `_logger.LogAppInsightsDependency(...)` → **dependencies** under this request.
   - **Exceptions** – `_logger.LogError(ex, ...)` or `_logger.LogAppInsightsException(...)` → **exceptions** under this request.
4. **Set request-level fields** on `operation.Telemetry` (e.g. `Success`, `ResponseCode`, `Properties["CorrelationId"]`) before the block ends.

The Application Insights SDK assigns an **operation ID** to the request when you call `StartOperation`. All telemetry emitted inside the block (from ILogger/Serilog and from `TelemetryClient.Track*`) is correlated with that same operation ID, so in the portal you see one request with its duration and, under it, all related traces, events, metrics, dependencies, and exceptions.

**Example:** One background job = one RequestTelemetry “ProcessJob” with duration and success; all `_logger` calls and `LogAppInsightsEvent` / `LogAppInsightsMetric` / `LogAppInsightsDependency` calls inside the `using` block appear under that request in the transaction view.

---

For **long-running or background operations** where you want:

- A **single request** in Application Insights representing the whole operation,
- **All logs and telemetry** inside that operation correlated under one operation ID,
- **Rich properties** and explicit success/response code,

inject **`TelemetryClient`** and use **`StartOperation<RequestTelemetry>`**. This is the same pattern as the Application Insights SDK usage in other projects (e.g. TaxFormGenerator).

### 6.1 Registration

`TelemetryClient` is registered when **`AddSerilogApplicationInsights`** runs (it calls **`AddApplicationInsightsTelemetry`**). Inject it where needed:

```csharp
public class MyBackgroundHandler
{
    private readonly ILogger<MyBackgroundHandler> _logger;
    private readonly TelemetryClient _telemetryClient;

    public MyBackgroundHandler(
        ILogger<MyBackgroundHandler> logger,
        TelemetryClient telemetryClient)
    {
        _logger = logger;
        _telemetryClient = telemetryClient;
    }
}
```

### 6.2 Wrapping an operation (TelemetryClient)

```csharp
public async Task ProcessAsync(Guid correlationId, CancellationToken cancellationToken)
{
    using var operation = _telemetryClient.StartOperation<RequestTelemetry>("ProcessJob");

    operation.Telemetry.Properties["CorrelationId"] = correlationId.ToString();

    try
    {
        _logger.LogInformation("Starting job {CorrelationId}.", correlationId);
        _logger.LogAppInsightsEvent("JobStarted", properties: new Dictionary<string, object?> { ["CorrelationId"] = correlationId.ToString() });
        await DoWorkAsync(correlationId, cancellationToken);
        _logger.LogAppInsightsMetric("ItemsProcessed", itemCount); // or: operation.Telemetry.Properties["ItemsProcessed"] = itemCount;
        operation.Telemetry.Success = true;
        operation.Telemetry.ResponseCode = "200";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Job {CorrelationId} failed.", correlationId);
        operation.Telemetry.Success = false;
        operation.Telemetry.ResponseCode = "500";
        operation.Telemetry.Properties["Error"] = ex.Message;
        _telemetryClient.TrackException(ex);
        throw;
    }
}
```

- **`StartOperation<RequestTelemetry>("ProcessJob")`** creates a logical request; when the `using` block ends, it is sent with duration and success/response code.
- **All telemetry** emitted inside the `using` block—logs (traces), **events** (`LogAppInsightsEvent`), **metrics** (`LogAppInsightsMetric`), dependencies (`LogAppInsightsDependency`), and exceptions—are associated with this request (same operation ID) in Application Insights.
- You can set **Success**, **ResponseCode**, and **Properties** on `operation.Telemetry` for filtering and failure analysis.

**Why `LogAppInsightsMetric("ItemsProcessed", itemCount)` instead of `operation.Telemetry.Properties["ItemsProcessed"] = itemCount`?**
- **`LogAppInsightsMetric`** sends **MetricTelemetry**: a numeric metric that appears in **Metrics Explorer**, supports aggregation (sum, average, count) and charts across many requests, and can be used for alerts. It is still correlated with this request when emitted inside the `using` block.
- **`operation.Telemetry.Properties["ItemsProcessed"] = ...`** adds a **custom dimension** (property) on the **request only**. You can filter and search by it, but it does not show up as an aggregate metric. Use **Properties** when you only need the value as a dimension on this request; use **LogAppInsightsMetric** when you want metric semantics (aggregation, trends, dashboards).

Use this for background jobs, workers, or any unit of work you want to see as one request in the portal. For simple one-off metrics or events, the ILogger extensions (section 5) are enough.

### 6.3 Wrapping an operation (ILogger only)

Same flow and properties, but **without TelemetryClient**: use a `Stopwatch` (`System.Diagnostics`) and send one **Request** at the end via `LogAppInsightsRequest` with duration, success, response code, and properties (CorrelationId, Error). Events, metrics, and exception still go through ILogger.

```csharp
public async Task ProcessAsync(Guid correlationId, CancellationToken cancellationToken)
{
    var sw = Stopwatch.StartNew();
    var success = true;
    var responseCode = "200";
    string? errorMessage = null;

    try
    {
        _logger.LogInformation("Starting job {CorrelationId}.", correlationId);
        _logger.LogAppInsightsEvent("JobStarted", properties: new Dictionary<string, object?> { ["CorrelationId"] = correlationId.ToString() });
        await DoWorkAsync(correlationId, cancellationToken);
        _logger.LogAppInsightsMetric("ItemsProcessed", itemCount); // itemCount from your logic
    }
    catch (Exception ex)
    {
        success = false;
        responseCode = "500";
        errorMessage = ex.Message;
        _logger.LogAppInsightsException(ex, "Job {CorrelationId} failed.", new Dictionary<string, object?> { ["CorrelationId"] = correlationId.ToString() });
        throw;
    }
    finally
    {
        sw.Stop();
        var properties = new Dictionary<string, object?> { ["CorrelationId"] = correlationId.ToString() };
        if (errorMessage != null)
            properties["Error"] = errorMessage;
        _logger.LogAppInsightsRequest(
            requestName: "ProcessJob",
            durationMs: sw.ElapsedMilliseconds,
            responseCode: responseCode,
            success: success,
            requestId: correlationId.ToString(),
            properties: properties);
    }
}
```

All telemetry (event, metric, exception, request) uses **ILogger** only.

- **No TelemetryClient**—only `ILogger`. The logical request is sent as a single **Request** via `LogAppInsightsRequest` in `finally`, with the same **properties** (CorrelationId, and Error when failed).
- **Trade-off:** Telemetry emitted inside the block (traces, events, metrics, exception) is **not** under the same operation ID as the request in Application Insights; you get one request plus separate traces/events/metrics/exception. For full correlation (one operation ID for everything), use `StartOperation<RequestTelemetry>` (see "How to consolidate…" above or section 6 intro).
- Use this when you want a single request with all properties and don't need operation-level correlation.

---

## 7. Property Keys and Limits

### 7.1 All TelemetryType values

The converter supports exactly **six** values for the **`TelemetryType`** property (defined in `ApplicationInsightsPropertyKeys.TelemetryTypes`). All are implemented and documented:

| TelemetryType | Application Insights type | How it is produced |
|---------------|----------------------------|---------------------|
| **Event** | EventTelemetry | `LogAppInsightsEvent` (section 5.1) or scope with `TelemetryType = "Event"` and `EventName`. |
| **Metric** | MetricTelemetry | `LogAppInsightsMetric` (section 5.2) or scope with `TelemetryType = "Metric"`, `MetricName`, `MetricValue`. |
| **Dependency** | DependencyTelemetry | `LogAppInsightsDependency` (section 5.3) or scope with `TelemetryType = "Dependency"` and dependency properties. |
| **Request** | RequestTelemetry | `LogAppInsightsRequest` (section 5.4) or scope with `TelemetryType = "Request"` and request properties. |
| **Exception** | ExceptionTelemetry | `LogAppInsightsException` (section 5.5), or `LogError(ex, ...)` (converter turns it into Exception when exception object present), or scope with `TelemetryType = "Exception"`. |
| **Trace** | TraceTelemetry | Default when `TelemetryType` is missing or not one of the above. Normal `_logger.LogInformation(...)` etc. become Trace. You can also send trace telemetry directly via `TelemetryClient.TrackTrace(message, severityLevel, properties)` (see [TrackTrace - Classic API](https://learn.microsoft.com/en-us/azure/azure-monitor/app/classic-api?tabs=dotnet#tracktrace)); in this app ILogger → Serilog → converter is the standard path. |

### 7.2 Reserved property keys

The converter uses these keys to build the telemetry item. They are **not** copied to custom dimensions (see `ApplicationInsightsPropertyKeys.ReservedKeys`):

| Key | Used for |
|-----|----------|
| **TelemetryType** | Event, Metric, Dependency, Exception, Request, Trace |
| **EventName** | Event name |
| **Metric_** prefix | Event metrics (e.g. `Metric_Count` → `EventTelemetry.Metrics["Count"]`) |
| **MetricName**, **MetricValue** | Metric name and value |
| **DependencyTypeName**, **DependencyTarget**, **DependencyName**, **DependencyData**, **DependencyResultCode** | Dependency fields |
| **DurationMs**, **Success** | Dependency and Request |
| **RequestName**, **RequestUrl**, **RequestId**, **ResponseCode** | Request fields |
| **ExceptionMessage** | Exception message when no exception object is provided |

Any other property you add (e.g. in `properties` dictionaries) is copied to the telemetry item’s **Properties** (custom dimensions), subject to limits below.

### 7.3 Application Insights limits (enforced by converter)

Limits follow the [Application Insights telemetry limits](https://learn.microsoft.com/en-us/azure/azure-monitor/app/classic-api?tabs=dotnet#limits):

- **Trace/exception message length:** 32,768 characters (`ApplicationInsightsPropertyKeys.MaxMessageLength`). Longer messages are truncated. Per the [TrackTrace](https://learn.microsoft.com/en-us/azure/azure-monitor/app/classic-api?tabs=dotnet#tracktrace) docs, the message size limit is much higher than for property values, so trace messages can hold longer diagnostic data (e.g. encoded POST data) when needed.
- **Property value length:** 8,192 characters (`ApplicationInsightsPropertyKeys.MaxPropertyValueLength`). Longer values are truncated.

Avoid putting large payloads (e.g. full request bodies) in messages or properties; send only what you need for diagnostics.

---

## 8. Viewing Data in Azure Portal

1. Open the **Application Insights** resource for this app (using the connection string in config).
2. **Logs (Analytics)**
   - **traces** – Standard logs and custom trace telemetry.
   - **customEvents** – Events sent via `LogAppInsightsEvent`.
   - **customMetrics** – Metrics sent via `LogAppInsightsMetric` (and event metrics).
   - **dependencies** – Automatic and custom dependencies (`LogAppInsightsDependency`).
   - **requests** – Custom requests (`LogAppInsightsRequest`, `StartOperation<RequestTelemetry>`), or per-HTTP **Request** rows if you enable the SDK request module. When an HTTP context exists, **customDimensions** can include CorrelationId, TaxYear, RequestPath, ClientIp, Application, Module, and Env (from **`RequestContextTelemetryInitializer`** and config).
   - **exceptions** – Exceptions (automatic and `LogAppInsightsException`).
3. **Transaction search** – Search by operation ID, request name, or custom dimensions.
4. **Live Metrics** – Near real-time stream when `EnableQuickPulseMetricStream` is true.
5. **Failures / Performance** – Use the built-in views for failed requests and slow dependencies.

Example Kusto (Logs):

```kusto
customEvents
| where timestamp > ago(1h)
| project timestamp, name, customDimensions, customMeasurements

customMetrics
| where name == "OrdersPerMinute"
| summarize avg(value) by bin(timestamp, 30s)
```

---

## 9. Best Practices

1. **Use structured logging** – Prefer `_logger.LogInformation("Processed {Count} items.", count)` over string concatenation so you can query by `Count` in Application Insights.
2. **Choose the right telemetry type** – Use events for business occurrences, metrics for numbers over time, dependencies for outbound calls, requests for logical operations, exceptions for failures.
3. **Don’t log PII** – Avoid putting personally identifiable information in messages or properties.
4. **Use scopes** – Attach correlation IDs or request-specific data with `BeginScope` so all logs in that block are tagged.
5. **Prefer ILogger extensions** – For most custom telemetry, use `LogAppInsightsEvent`, `LogAppInsightsMetric`, etc. Use `TelemetryClient.StartOperation` when you need one correlated “request” and full context for a whole operation.
6. **Keep connection string secure** – Use `APPLICATIONINSIGHTS_CONNECTION_STRING` in production and avoid committing secrets.
7. **Tune sampling** – If volume is high, enable `EnableAdaptiveSampling` or adjust sampling in code so you keep representative traces without excessive cost.
8. **Global dimensions** – Application, Module, and Env come from **`Serilog:Properties`** and are applied to **`TelemetryClient.Context.GlobalProperties`** in **`UseTelemetryBaseInitializer()`**; use them for filtering by environment or component. Prefer **`APPLICATIONINSIGHTS_CONNECTION_STRING`** (and avoid committing real connection strings) in non-local environments.

---

## Quick Reference: Where to Implement What

| Goal | How to implement |
|------|-------------------|
| Log a message (info, warning, error) | `_logger.LogInformation(...)` (or other levels). |
| Log with exception | `_logger.LogError(exception, "Message");` |
| Custom event (e.g. “OrderSubmitted”) | `_logger.LogAppInsightsEvent("OrderSubmitted", properties, metrics);` |
| Single numeric metric | `_logger.LogAppInsightsMetric("MetricName", value, properties);` |
| Outbound call (HTTP, SQL, etc.) | `_logger.LogAppInsightsDependency(typeName, target, name, durationMs, success, data, resultCode);` |
| Logical request (e.g. background job) | `_logger.LogAppInsightsRequest(name, durationMs, responseCode, success, url, requestId);` or `TelemetryClient.StartOperation<RequestTelemetry>("Name")` for full correlation. |
| Per-HTTP **Request** row when SDK module is off | Register **`ApplicationInsightsRequestLoggingMiddleware`** (see §2.4 optional block); **`success`** follows `status < 400`. |
| Report exception to App Insights | `_logger.LogAppInsightsException(exception, message, properties);` |
| One request with full correlation | Inject `TelemetryClient`, use `using var op = _telemetryClient.StartOperation<RequestTelemetry>("OpName");` and set `op.Telemetry` properties and Success/ResponseCode. |
| CorrelationId, TaxYear, etc. on telemetry when HTTP context exists | Handled by **`RequestContextTelemetryInitializer`** (section 2.5); no code changes needed. |

All extension methods are in **`Serilog.Sinks.ApplicationInsights.AspNetCore.Core.LoggerApplicationInsightsExtensions`**; add **`using Serilog.Sinks.ApplicationInsights.AspNetCore.Core;`** (or a project-level global using) where you call them.

---

## References

- [Monitor .NET and Node.js with Application Insights (Classic API)](https://learn.microsoft.com/en-us/azure/azure-monitor/app/classic-api) – Telemetry types, TrackTrace, TrackEvent, TrackMetric, limits, and operation context.
- [TrackTrace](https://learn.microsoft.com/en-us/azure/azure-monitor/app/classic-api?tabs=dotnet#tracktrace) – Trace telemetry, message vs property length, and the `traces` table in Log Analytics.
- [Application Insights limits](https://learn.microsoft.com/en-us/azure/azure-monitor/app/classic-api?tabs=dotnet#limits) – Message length (32,768), property value length (8,192), and other quotas.
