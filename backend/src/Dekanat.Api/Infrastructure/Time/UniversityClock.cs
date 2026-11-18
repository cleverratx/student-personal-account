using Microsoft.Extensions.Options;

namespace Dekanat.Api.Infrastructure.Time
{
    /// <summary>
    /// Настройки вуза из секции University. Часовой пояс задаётся идентификатором IANA, например Europe/Moscow.
    /// </summary>
    public sealed class UniversityOptions
    {
        public const string Section = "University";

        public string TimeZone { get; set; } = "Europe/Moscow";
    }

    /// <summary>
    /// Время вуза. В базе время пар хранится в UTC, а «день», «неделя» и «сегодня» считаются во времени вуза:
    /// пара в 01:00 по Москве относится к своему дню, хотя в UTC это ещё вчера.
    /// </summary>
    public sealed class UniversityClock
    {
        // Москва живёт в UTC+3 без перехода на летнее время с 2014 года. Этот пояс нужен, только если
        // в системе нет базы часовых поясов: тогда сервер продолжит работать, а не упадёт при старте.
        private static readonly TimeSpan MoscowOffset = TimeSpan.FromHours( 3 );

        private readonly TimeProvider _time;

        public UniversityClock( IOptions<UniversityOptions> options, TimeProvider time, ILogger<UniversityClock> logger )
        {
            _time = time;
            ZoneId = options.Value.TimeZone;
            Zone = FindZone( ZoneId, logger );
        }

        /// <summary>Идентификатор пояса, его получает фронт, чтобы показывать время так же.</summary>
        public string ZoneId { get; }

        public TimeZoneInfo Zone { get; }

        public DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

        /// <summary>Сегодняшняя дата во времени вуза.</summary>
        public DateOnly Today => DateOnly.FromDateTime( TimeZoneInfo.ConvertTimeFromUtc( UtcNow, Zone ) );

        /// <summary>Дата и время по часам вуза в UTC для базы.</summary>
        public DateTime ToUtc( DateOnly date, TimeOnly time )
        {
            return TimeZoneInfo.ConvertTimeToUtc( date.ToDateTime( time, DateTimeKind.Unspecified ), Zone );
        }

        private static TimeZoneInfo FindZone( string id, ILogger logger )
        {
            if ( TimeZoneInfo.TryFindSystemTimeZoneById( id, out TimeZoneInfo? zone ) )
            {
                return zone;
            }

            if ( id == "Europe/Moscow" )
            {
                logger.LogWarning( "В системе нет базы часовых поясов, для {TimeZone} берём UTC+3", id );
                return TimeZoneInfo.CreateCustomTimeZone( id, MoscowOffset, id, id );
            }

            throw new InvalidOperationException( $"Часовой пояс вуза {id} не найден. Укажите идентификатор IANA в University:TimeZone." );
        }
    }

    public static class UniversityTimeSetup
    {
        public static IServiceCollection AddUniversityTime( this IServiceCollection services )
        {
            services.AddOptions<UniversityOptions>().BindConfiguration( UniversityOptions.Section );
            services.AddSingleton<UniversityClock>();
            return services;
        }
    }
}
