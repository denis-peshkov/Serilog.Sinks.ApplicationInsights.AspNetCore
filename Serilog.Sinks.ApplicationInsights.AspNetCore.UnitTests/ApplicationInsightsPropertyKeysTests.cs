namespace Serilog.Sinks.ApplicationInsights.AspNetCore.UnitTests;

[TestFixture]
public sealed class ApplicationInsightsPropertyKeysTests
{
    [Test]
    public void ReservedKeys_contains_all_structural_keys_and_is_case_insensitive()
    {
        ApplicationInsightsPropertyKeys.ReservedKeys.Should().Contain(ApplicationInsightsPropertyKeys.TelemetryType);
        ApplicationInsightsPropertyKeys.ReservedKeys.Should().Contain(ApplicationInsightsPropertyKeys.EventName);
        ApplicationInsightsPropertyKeys.ReservedKeys.Should().Contain("telemetrytype");
        ApplicationInsightsPropertyKeys.ReservedKeys.Comparer.Should().Be(StringComparer.OrdinalIgnoreCase);
    }

    [Test]
    public void TelemetryTypes_constants_are_distinct()
    {
        var types = new[]
        {
            ApplicationInsightsPropertyKeys.TelemetryTypes.Event,
            ApplicationInsightsPropertyKeys.TelemetryTypes.Metric,
            ApplicationInsightsPropertyKeys.TelemetryTypes.Dependency,
            ApplicationInsightsPropertyKeys.TelemetryTypes.Exception,
            ApplicationInsightsPropertyKeys.TelemetryTypes.Request,
            ApplicationInsightsPropertyKeys.TelemetryTypes.Trace,
        };
        types.Distinct().Should().HaveCount(types.Length);
    }

    [Test]
    public void Length_limits_are_positive()
    {
        ApplicationInsightsPropertyKeys.MaxMessageLength.Should().BeGreaterThan(0);
        ApplicationInsightsPropertyKeys.MaxPropertyValueLength.Should().BeGreaterThan(0);
    }
}
