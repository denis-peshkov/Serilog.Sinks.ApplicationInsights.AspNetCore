namespace Serilog.Sinks.ApplicationInsights.AspNetCore.UnitTests.Support;

/// <summary>Captures <see cref="Microsoft.Extensions.Logging.ILogger.BeginScope"/> payloads and log calls for assertions.</summary>
internal sealed class ListLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    public List<object?> ScopeStates { get; } = new();
    public List<(LogLevel Level, EventId EventId, string Message)> Entries { get; } = new();

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        ScopeStates.Add(state);
        return NullDisposable.Instance;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, eventId, formatter(state, exception)));
    }

    private sealed class NullDisposable : IDisposable
    {
        internal static readonly NullDisposable Instance = new();
        public void Dispose()
        {
        }
    }
}
