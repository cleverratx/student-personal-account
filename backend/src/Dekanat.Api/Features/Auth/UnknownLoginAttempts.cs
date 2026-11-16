using Microsoft.Extensions.Caching.Memory;

namespace Dekanat.Api.Features.Auth
{
    /// <summary>
    /// Неудачные попытки входа под несуществующим логином. Считаем их так же, как для настоящих учётных
    /// записей, и так же блокируем на пять минут: иначе по ответу 429 можно было бы понять, что логин существует.
    /// </summary>
    public sealed class UnknownLoginAttempts( IMemoryCache cache, TimeProvider time )
    {
        private sealed class Counter
        {
            public int Failures { get; set; }

            public DateTimeOffset? LockedUntil { get; set; }
        }

        /// <summary>
        /// Если логин сейчас «заблокирован», возвращает, сколько ждать. Иначе null.
        /// </summary>
        public TimeSpan? LockedFor( string normalizedLogin )
        {
            if ( !cache.TryGetValue( Key( normalizedLogin ), out Counter? counter ) || counter?.LockedUntil is null )
            {
                return null;
            }

            TimeSpan left = counter.LockedUntil.Value - time.GetUtcNow();
            return left > TimeSpan.Zero ? left : null;
        }

        /// <summary>
        /// Записывает неудачную попытку. Возвращает время блокировки, если попытка была пятой.
        /// </summary>
        public TimeSpan? RegisterFailure( string normalizedLogin )
        {
            Counter counter = cache.GetOrCreate( Key( normalizedLogin ), entry =>
            {
                entry.SlidingExpiration = IdentitySetup.LockoutDuration;
                return new Counter();
            } )!;

            lock ( counter )
            {
                counter.Failures++;
                if ( counter.Failures < IdentitySetup.MaxFailedAttempts )
                {
                    return null;
                }

                counter.Failures = 0;
                counter.LockedUntil = time.GetUtcNow() + IdentitySetup.LockoutDuration;
                return IdentitySetup.LockoutDuration;
            }
        }

        private static string Key( string normalizedLogin )
        {
            return $"unknown-login:{normalizedLogin}";
        }
    }
}
