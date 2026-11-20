using System.Net;
using System.Threading.RateLimiting;
using Dekanat.Api.Infrastructure.OpenApi;
using Dekanat.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Dekanat.Api.Features.Auth
{
    /// <summary>
    /// Ограничение частоты запросов к /api/auth/* с одного IP. В тестах лимит поднимают через конфигурацию.
    /// </summary>
    public sealed class AuthRateLimitOptions
    {
        public const string Section = "RateLimits:Auth";

        public int PermitPerMinute { get; set; } = 10;
    }

    public static class SessionSetup
    {
        public const string CookieName = "dekanat_session";
        public const string AuthRateLimitPolicy = "auth";
        public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays( 14 );

        /// <summary>
        /// Сессия в cookie: HttpOnly, Secure, SameSite=Lax, 14 дней со скользящим продлением.
        /// Всё под /api требует входа, кроме того, что явно помечено AllowAnonymous().
        /// </summary>
        public static IServiceCollection AddDekanatSession( this IServiceCollection services )
        {
            services.AddAuthentication( IdentityConstants.ApplicationScheme ).AddIdentityCookies();
            services.ConfigureApplicationCookie( options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = SessionLifetime;
                options.SlidingExpiration = true;

                // Это API, а не сайт со страницей входа: вместо редиректа отдаём код,
                // а ProblemDetails к нему добавляет UseStatusCodePages.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            } );

            IDataProtectionBuilder dataProtection = services.AddDataProtection().SetApplicationName( "dekanat" );
            // При сборке OpenAPI-документа базы нет, и чтение ключей из неё только сыпало бы ошибками в лог сборки.
            if ( !OpenApiSetup.IsGeneratingDocument )
            {
                dataProtection.PersistKeysToDbContext<AppDbContext>();
            }

            // Без входа политика отвечает 401, при чужой роли 403.
            services.AddAuthorizationBuilder()
                .SetFallbackPolicy( new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build() )
                .AddPolicy( AuthorizationPolicies.Student, policy => policy.RequireRole( Roles.Student ) )
                .AddPolicy( AuthorizationPolicies.Staff, policy => policy.RequireRole( Roles.Staff ) );

            services.AddMemoryCache();
            services.AddSingleton( TimeProvider.System );
            services.AddSingleton<UnknownLoginAttempts>();

            services.AddOptions<AuthRateLimitOptions>().BindConfiguration( AuthRateLimitOptions.Section );
            services.AddRateLimiter( options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy( AuthRateLimitPolicy, context =>
                {
                    int permit = context.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value.PermitPerMinute;
                    string ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter( ip, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permit,
                        Window = TimeSpan.FromMinutes( 1 ),
                        QueueLimit = 0,
                    } );
                } );
                options.OnRejected = WriteTooManyRequestsAsync;
            } );

            // За nginx адрес клиента приходит в X-Forwarded-For. Верим ему только от прокси из локальных
            // и docker-сетей, иначе лимит по IP легко обойти подделанным заголовком.
            services.Configure<ForwardedHeadersOptions>( options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Add( System.Net.IPNetwork.Parse( "127.0.0.0/8" ) );
                options.KnownIPNetworks.Add( System.Net.IPNetwork.Parse( "10.0.0.0/8" ) );
                options.KnownIPNetworks.Add( System.Net.IPNetwork.Parse( "172.16.0.0/12" ) );
                options.KnownIPNetworks.Add( System.Net.IPNetwork.Parse( "192.168.0.0/16" ) );
                options.KnownProxies.Add( IPAddress.IPv6Loopback );
            } );

            return services;
        }

        public static IApplicationBuilder UseDekanatSession( this IApplicationBuilder app )
        {
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
            return app;
        }

        private static async ValueTask WriteTooManyRequestsAsync( OnRejectedContext context, CancellationToken cancellationToken )
        {
            TimeSpan retryAfter = context.Lease.TryGetMetadata( MetadataName.RetryAfter, out TimeSpan value )
                ? value
                : TimeSpan.FromMinutes( 1 );
            await AuthProblems.TooManyAttempts( retryAfter ).ExecuteAsync( context.HttpContext );
        }
    }

    /// <summary>
    /// Ошибки входа в формате из docs/api.md.
    /// </summary>
    public static class AuthProblems
    {
        public static IResult InvalidCredentials()
        {
            return TypedResults.Problem(
                type: "https://dekanat.local/errors/invalid-credentials",
                title: "Не удалось войти",
                detail: "Неверный логин или пароль.",
                statusCode: StatusCodes.Status401Unauthorized );
        }

        public static IResult TooManyAttempts( TimeSpan retryAfter )
        {
            int seconds = Math.Max( 1, ( int )Math.Ceiling( retryAfter.TotalSeconds ) );
            return new TooManyAttemptsResult( seconds );
        }

        private sealed class TooManyAttemptsResult( int seconds ) : IResult
        {
            public Task ExecuteAsync( HttpContext httpContext )
            {
                httpContext.Response.Headers.RetryAfter = seconds.ToString( System.Globalization.CultureInfo.InvariantCulture );
                ProblemDetails problem = new()
                {
                    Type = "https://dekanat.local/errors/too-many-login-attempts",
                    Title = "Слишком много попыток входа",
                    Detail = "Слишком много попыток входа. Повторите позже.",
                    Status = StatusCodes.Status429TooManyRequests,
                };
                // Фронт показывает, сколько ждать, без разбора заголовка.
                problem.Extensions[ "retryAfterSeconds" ] = seconds;
                return TypedResults.Problem( problem ).ExecuteAsync( httpContext );
            }
        }
    }
}
