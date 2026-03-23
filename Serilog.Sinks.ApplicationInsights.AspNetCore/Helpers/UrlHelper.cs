namespace Serilog.Sinks.ApplicationInsights.AspNetCore.Helpers;

public static class UrlHelper
{
    public static bool IsPathExcludedFromValidation(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (string.IsNullOrEmpty(value) || value == "/")
            return true;
        if (value.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            return true;
        if (value.Equals("/health", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
