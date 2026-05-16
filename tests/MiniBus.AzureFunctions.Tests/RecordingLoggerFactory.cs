using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MiniBus.AzureFunctions.Tests;

internal sealed class RecordingLoggerFactory : ILoggerFactory
{
    private readonly AsyncLocal<Stack<IReadOnlyDictionary<string, object?>>> _scopes = new();

    public ConcurrentQueue<RecordingLogEntry> Entries { get; } = new();

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new RecordingLogger(this, categoryName);
    }

    public void Dispose()
    {
    }

    private IDisposable PushScope(IReadOnlyDictionary<string, object?> scope)
    {
        _scopes.Value ??= new Stack<IReadOnlyDictionary<string, object?>>();
        _scopes.Value.Push(scope);
        return new ScopePopper(_scopes.Value);
    }

    private IReadOnlyList<IReadOnlyDictionary<string, object?>> CaptureScopes()
    {
        return _scopes.Value?.Reverse().ToArray() ?? Array.Empty<IReadOnlyDictionary<string, object?>>();
    }

    private static IReadOnlyDictionary<string, object?> CaptureState<TState>(TState state)
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            return pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["State"] = state
        };
    }

    private sealed class RecordingLogger : ILogger
    {
        private readonly RecordingLoggerFactory _factory;
        private readonly string _categoryName;

        public RecordingLogger(
            RecordingLoggerFactory factory,
            string categoryName)
        {
            _factory = factory;
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return _factory.PushScope(CaptureState(state));
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _factory.Entries.Enqueue(new RecordingLogEntry(
                _categoryName,
                logLevel,
                eventId,
                CaptureState(state),
                _factory.CaptureScopes(),
                exception,
                formatter(state, exception)));
        }
    }

    private sealed class ScopePopper : IDisposable
    {
        private readonly object _lockObject = new object();
        private readonly Stack<IReadOnlyDictionary<string, object?>> _scopes;
        private bool _disposed;

        public ScopePopper(Stack<IReadOnlyDictionary<string, object?>> scopes)
        {
            _scopes = scopes;
        }

        public void Dispose()
        {
            lock (_lockObject)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                if (_scopes.Count > 0)
                {
                    _scopes.Pop();
                }
            }
        }
    }
}

internal sealed record RecordingLogEntry(
    string CategoryName,
    LogLevel Level,
    EventId EventId,
    IReadOnlyDictionary<string, object?> State,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Scopes,
    Exception? Exception,
    string Message);
