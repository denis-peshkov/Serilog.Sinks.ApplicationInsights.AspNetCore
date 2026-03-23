namespace Serilog.Sinks.ApplicationInsights.AspNetCore.UnitTests.Support;

/// <summary>Temporarily assigns <see cref="Activity.Current"/> (restore on dispose).</summary>
internal sealed class ActivityCurrentReplacer : IDisposable
{
    private readonly Activity? _previous;

    public ActivityCurrentReplacer(Activity? activity)
    {
        _previous = Activity.Current;
        Activity.Current = activity;
    }

    public void Dispose() => Activity.Current = _previous;
}
