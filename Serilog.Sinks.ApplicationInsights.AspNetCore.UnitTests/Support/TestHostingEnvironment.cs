namespace Serilog.Sinks.ApplicationInsights.AspNetCore.UnitTests.Support;

#pragma warning disable CS0618 // IHostingEnvironment still required by Application Insights DefaultApplicationInsightsServiceConfigureOptions in bare ServiceCollection tests.

/// <summary>
/// Minimal <see cref="Microsoft.AspNetCore.Hosting.IHostingEnvironment"/> for DI tests that call <c>AddApplicationInsightsTelemetry</c>
/// without a full web host.
/// </summary>
internal sealed class TestHostingEnvironment : Microsoft.AspNetCore.Hosting.IHostingEnvironment
{
    public string ApplicationName { get; set; } = "UnitTests";

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public string EnvironmentName { get; set; } = "Development";

    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

    public string WebRootPath { get; set; } = string.Empty;
}

#pragma warning restore CS0618
