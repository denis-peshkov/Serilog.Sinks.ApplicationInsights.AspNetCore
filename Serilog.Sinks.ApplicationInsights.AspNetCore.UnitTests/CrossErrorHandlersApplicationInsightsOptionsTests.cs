namespace Serilog.Sinks.ApplicationInsights.AspNetCore.UnitTests;

[TestFixture]
public sealed class CrossErrorHandlersApplicationInsightsOptionsTests
{
    [Test]
    public void Default_options_enable_enrichment_and_version_property()
    {
        var o = new CrossErrorHandlersApplicationInsightsOptions();
        o.EnrichOperationFromActivity.Should().BeTrue();
        o.AddExtensionVersionProperty.Should().BeTrue();
    }
}
