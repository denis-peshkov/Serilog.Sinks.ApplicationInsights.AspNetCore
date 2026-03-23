namespace Serilog.Sinks.ApplicationInsights.AspNetCore.UnitTests;

[TestFixture]
public sealed class ServiceCollectionExtensionsTests
{
#pragma warning disable CS0618 // IHostingEnvironment required by Application Insights when building ServiceProvider without WebHost.
    private static void AddMinimalHostingForApplicationInsights(IServiceCollection services)
    {
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IHostingEnvironment>(new TestHostingEnvironment());
    }
#pragma warning restore CS0618

    [Test]
    public void AddCrossErrorHandlersApplicationInsights_throws_when_services_null()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            ServiceCollectionExtensions.AddCrossErrorHandlersApplicationInsights(null!));
        ex!.ParamName.Should().Be("services");
    }

    [Test]
    public void AddCrossErrorHandlersApplicationInsights_registers_two_initializers()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddCrossErrorHandlersApplicationInsights();
        using var sp = services.BuildServiceProvider();
        var list = sp.GetServices<ITelemetryInitializer>().ToList();
        list.Should().HaveCount(2);
        list.OfType<W3cActivityTelemetryInitializer>().Should().ContainSingle();
        list.OfType<ExtensionVersionTelemetryInitializer>().Should().ContainSingle();
    }

    [Test]
    public void AddSerilogApplicationInsights_registers_telemetry_client()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApplicationInsights:ConnectionString"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000",
        }).Build();
        var services = new ServiceCollection();
        AddMinimalHostingForApplicationInsights(services);
        services.AddLogging();
        services.AddSerilogApplicationInsights(configuration);
        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<TelemetryClient>().Should().NotBeNull();
    }

    [Test]
    public void AddApplicationInsightsAzureAD_does_not_configure_credential_when_disabled()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApplicationInsights:UseAzureCredential"] = "false",
        }).Build();
        var services = new ServiceCollection();
        AddMinimalHostingForApplicationInsights(services);
        services.AddApplicationInsightsTelemetry();
        services.AddApplicationInsightsAzureAD(configuration);
        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<TelemetryConfiguration>().Should().NotBeNull();
    }

    [Test]
    public void AddApplicationInsightsAzureAD_registers_extra_services_when_credential_enabled()
    {
        static int DescriptorDelta(bool useCredential)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationInsights:UseAzureCredential"] = useCredential ? "true" : "false",
            }).Build();
            var services = new ServiceCollection();
            AddMinimalHostingForApplicationInsights(services);
            services.AddApplicationInsightsTelemetry();
            var before = services.Count;
            services.AddApplicationInsightsAzureAD(configuration);
            return services.Count - before;
        }

        DescriptorDelta(true).Should().BeGreaterThan(DescriptorDelta(false));
    }

    [Test]
    public void AddSerilogApplicationInsights_sets_request_tracking_when_enabled()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApplicationInsights:ConnectionString"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000",
        }).Build();
        var services = new ServiceCollection();
        AddMinimalHostingForApplicationInsights(services);
        services.AddLogging();
        services.AddSerilogApplicationInsights(configuration, enableRequestTrackingTelemetry: true);
        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<IOptions<ApplicationInsightsServiceOptions>>().Value.EnableRequestTrackingTelemetryModule
            .Should().BeTrue();
    }
}
