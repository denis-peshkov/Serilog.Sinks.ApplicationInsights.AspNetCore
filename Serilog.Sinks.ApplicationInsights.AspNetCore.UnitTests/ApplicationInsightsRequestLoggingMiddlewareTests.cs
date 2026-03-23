namespace Serilog.Sinks.ApplicationInsights.AspNetCore.UnitTests;

[TestFixture]
public sealed class ApplicationInsightsRequestLoggingMiddlewareTests
{
    [Test]
    public async Task InvokeAsync_skips_logging_for_excluded_paths()
    {
        var sink = new ListLogger<ApplicationInsightsRequestLoggingMiddleware>();
        ILogger<ApplicationInsightsRequestLoggingMiddleware> logger = sink;
        var middleware = new ApplicationInsightsRequestLoggingMiddleware(_ => Task.CompletedTask, logger);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/health";
        ctx.Response.StatusCode = StatusCodes.Status200OK;

        await middleware.InvokeAsync(ctx);

        sink.ScopeStates.Should().BeEmpty();
    }

    [Test]
    public async Task InvokeAsync_logs_request_and_metric_with_success_for_2xx()
    {
        var sink = new ListLogger<ApplicationInsightsRequestLoggingMiddleware>();
        ILogger<ApplicationInsightsRequestLoggingMiddleware> logger = sink;
        var middleware = new ApplicationInsightsRequestLoggingMiddleware(_ => Task.CompletedTask, logger);

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("localhost", 8443);
        ctx.Request.Path = "/api/x";
        ctx.Response.StatusCode = StatusCodes.Status200OK;

        await middleware.InvokeAsync(ctx);

        sink.ScopeStates.Should().HaveCount(2);
        var requestScope = ToScopeMap(sink.ScopeStates[0]);
        requestScope[ApplicationInsightsPropertyKeys.TelemetryType].Should().Be(ApplicationInsightsPropertyKeys.TelemetryTypes.Request);
        requestScope[ApplicationInsightsPropertyKeys.Success].Should().Be(true);
        requestScope[ApplicationInsightsPropertyKeys.ResponseCode].Should().Be("200");

        var metricScope = ToScopeMap(sink.ScopeStates[1]);
        metricScope[ApplicationInsightsPropertyKeys.TelemetryType].Should().Be(ApplicationInsightsPropertyKeys.TelemetryTypes.Metric);
    }

    [Test]
    public async Task InvokeAsync_marks_failure_for_4xx()
    {
        var sink = new ListLogger<ApplicationInsightsRequestLoggingMiddleware>();
        ILogger<ApplicationInsightsRequestLoggingMiddleware> logger = sink;
        var middleware = new ApplicationInsightsRequestLoggingMiddleware(_ => Task.CompletedTask, logger);

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Scheme = "http";
        ctx.Request.Host = new HostString("localhost");
        ctx.Request.Path = "/missing";
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;

        await middleware.InvokeAsync(ctx);

        var requestScope = ToScopeMap(sink.ScopeStates[0]);
        requestScope[ApplicationInsightsPropertyKeys.Success].Should().Be(false);
        requestScope[ApplicationInsightsPropertyKeys.ResponseCode].Should().Be("404");
    }

    [Test]
    public async Task InvokeAsync_still_logs_after_next_throws()
    {
        var sink = new ListLogger<ApplicationInsightsRequestLoggingMiddleware>();
        ILogger<ApplicationInsightsRequestLoggingMiddleware> logger = sink;
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("localhost");
        ctx.Request.Path = "/api/risky";
        var middleware = new ApplicationInsightsRequestLoggingMiddleware(
            _ =>
            {
                ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return Task.FromException(new IOException("pipeline failed"));
            },
            logger);

        try
        {
            await middleware.InvokeAsync(ctx);
            Assert.Fail("Expected IOException from pipeline.");
        }
        catch (IOException)
        {
        }

        sink.ScopeStates.Should().HaveCount(2);
        var requestScope = ToScopeMap(sink.ScopeStates[0]);
        requestScope[ApplicationInsightsPropertyKeys.ResponseCode].Should().Be("500");
        requestScope[ApplicationInsightsPropertyKeys.Success].Should().Be(false);
    }

    [Test]
    public void Constructor_throws_on_null_dependencies()
    {
        var logger = new Mock<ILogger<ApplicationInsightsRequestLoggingMiddleware>>();
        Assert.Throws<ArgumentNullException>(() =>
            new ApplicationInsightsRequestLoggingMiddleware(null!, logger.Object));
        Assert.Throws<ArgumentNullException>(() =>
            new ApplicationInsightsRequestLoggingMiddleware(_ => Task.CompletedTask, null!));
    }

    private static Dictionary<string, object?> ToScopeMap(object? state)
    {
        if (state is Dictionary<string, object?> d0)
            return new Dictionary<string, object?>(d0, StringComparer.OrdinalIgnoreCase);
        if (state is Dictionary<string, object> d1)
            return d1.ToDictionary(kv => kv.Key, kv => (object?)kv.Value, StringComparer.OrdinalIgnoreCase);
        throw new InvalidOperationException(state?.GetType().FullName);
    }
}
