namespace Serilog.Sinks.ApplicationInsights.AspNetCore.UnitTests;

[TestFixture]
public sealed class UrlHelperTests
{
    [TestCase("/", true)]
    [TestCase("/swagger", true)]
    [TestCase("/swagger/index.html", true)]
    [TestCase("/health", true)]
    [TestCase("", true)]
    [TestCase("/api/values", false)]
    public void IsPathExcludedFromValidation_returns_expected(string pathValue, bool expected)
    {
        var path = new PathString(pathValue);
        UrlHelper.IsPathExcludedFromValidation(path).Should().Be(expected);
    }
}
