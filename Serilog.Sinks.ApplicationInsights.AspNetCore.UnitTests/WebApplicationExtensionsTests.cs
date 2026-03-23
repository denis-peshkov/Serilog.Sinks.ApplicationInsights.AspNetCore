namespace Serilog.Sinks.ApplicationInsights.AspNetCore.UnitTests;

[TestFixture]
public sealed class WebApplicationExtensionsTests
{
    [Test]
    public void UseTelemetryBaseInitializer_throws_when_app_null()
    {
        WebApplication? app = null;
        var ex = Assert.Throws<ArgumentNullException>(() => app!.UseTelemetryBaseInitializer());
        ex!.ParamName.Should().Be("app");
    }

    [Test]
    public async Task UseTelemetryBaseInitializer_sets_global_properties_from_configuration()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = Array.Empty<string>() });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApplicationInsights:ConnectionString"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000",
            ["Serilog:Properties:Application"] = "AppFromConfig",
            ["Serilog:Properties:Module"] = "ModuleFromConfig",
            ["Serilog:Properties:Env"] = "EnvFromConfig",
        });
        builder.Services.AddSerilogApplicationInsights(builder.Configuration);
        await using var app = builder.Build();
        app.UseTelemetryBaseInitializer();
        var client = app.Services.GetRequiredService<TelemetryClient>();
        client.Context.GlobalProperties["Application"].Should().Be("AppFromConfig");
        client.Context.GlobalProperties["Module"].Should().Be("ModuleFromConfig");
        client.Context.GlobalProperties["Env"].Should().Be("EnvFromConfig");
    }
}
