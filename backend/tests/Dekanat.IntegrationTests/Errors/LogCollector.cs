using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Dekanat.IntegrationTests.Errors
{
    /// <summary>
    /// Логгер для тестов: запоминает записи вместе со scope, чтобы проверить, что в лог попал RequestId.
    /// </summary>
    public sealed class LogCollector : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public ILogger CreateLogger( string categoryName )
        {
            return new CollectingLogger( this, categoryName );
        }

        public void SetScopeProvider( IExternalScopeProvider scopeProvider )
        {
            _scopes = scopeProvider;
        }

        public void Dispose()
        {
        }

        private sealed class CollectingLogger( LogCollector owner, string category ) : ILogger
        {
            public IDisposable? BeginScope<TState>( TState state ) where TState : notnull
            {
                return owner._scopes.Push( state );
            }

            public bool IsEnabled( LogLevel logLevel )
            {
                return true;
            }

            public void Log<TState>( LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter )
            {
                Dictionary<string, object?> scope = [];
                owner._scopes.ForEachScope( ( item, values ) =>
                {
                    if ( item is IEnumerable<KeyValuePair<string, object>> pairs )
                    {
                        foreach ( KeyValuePair<string, object> pair in pairs )
                        {
                            values[ pair.Key ] = pair.Value;
                        }
                    }
                }, scope );

                owner.Entries.Enqueue( new LogEntry( category, logLevel, formatter( state, exception ), exception, scope ) );
            }
        }
    }

    public sealed record LogEntry(
        string Category,
        LogLevel Level,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Scope );
}
