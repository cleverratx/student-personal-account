using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dekanat.Api.Features.Auth;
using Dekanat.IntegrationTests.Errors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Dekanat.IntegrationTests.Auth
{
    public sealed class SessionTests( DekanatApiFactory factory )
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Fact]
        public async Task LoginSetsSafeSessionCookieAndLogoutEndsSession()
        {
            TestUser user = await TestUsers.CreateAsync( factory.Services, Roles.Staff );
            HttpClient client = factory.CreateBrowser();

            HttpResponseMessage login = await client.LoginAsync( user.Login, user.Password );

            Assert.Equal( HttpStatusCode.OK, login.StatusCode );
            using JsonDocument body = JsonDocument.Parse( await login.Content.ReadAsStringAsync( Token ) );
            Assert.Equal( "Staff", body.RootElement.GetProperty( "role" ).GetString() );

            string cookie = Assert.Single( login.Headers.GetValues( "Set-Cookie" ), value => value.StartsWith( SessionSetup.CookieName, StringComparison.Ordinal ) );
            string flags = cookie.ToLowerInvariant();
            Assert.Contains( "httponly", flags, StringComparison.Ordinal );
            Assert.Contains( "secure", flags, StringComparison.Ordinal );
            Assert.Contains( "samesite=lax", flags, StringComparison.Ordinal );
            DateTimeOffset expires = ReadExpires( cookie );
            Assert.InRange( expires - DateTimeOffset.UtcNow, TimeSpan.FromDays( 13.9 ), TimeSpan.FromDays( 14.1 ) );

            MeResponse? me = await client.GetFromJsonAsync<MeResponse>( "/api/me", JsonOptions, Token );
            Assert.NotNull( me );
            Assert.Equal( user.Id, me.Id );
            Assert.Equal( user.Login, me.Login );
            Assert.Equal( UserRole.Staff, me.Role );

            HttpResponseMessage logout = await client.PostAsync( "/api/auth/logout", content: null, Token );
            Assert.Equal( HttpStatusCode.NoContent, logout.StatusCode );

            HttpResponseMessage afterLogout = await client.GetAsync( "/api/me", Token );
            Assert.Equal( HttpStatusCode.Unauthorized, afterLogout.StatusCode );
        }

        [Fact]
        public async Task WithoutCookieApiAnswers401WithoutRedirect()
        {
            HttpClient client = factory.CreateBrowser();

            HttpResponseMessage response = await client.GetAsync( "/api/me", Token );

            Assert.Equal( HttpStatusCode.Unauthorized, response.StatusCode );
            Assert.Null( response.Headers.Location );
            using Problem problem = await Problem.ReadAsync( response );
            Assert.Equal( "application/problem+json", problem.ContentType );
            Assert.Equal( "Нужно войти в кабинет", problem.Title );
        }

        [Fact]
        public async Task WrongPasswordAndUnknownLoginLookTheSame()
        {
            TestUser user = await TestUsers.CreateAsync( factory.Services );
            HttpClient client = factory.CreateBrowser();

            HttpResponseMessage wrongPassword = await client.LoginAsync( user.Login, "Wrong-password-1" );
            HttpResponseMessage unknownLogin = await client.LoginAsync( $"nobody.{Guid.NewGuid():N}"[ ..20 ], "Wrong-password-1" );

            Assert.Equal( HttpStatusCode.Unauthorized, wrongPassword.StatusCode );
            Assert.Equal( HttpStatusCode.Unauthorized, unknownLogin.StatusCode );
            using Problem first = await Problem.ReadAsync( wrongPassword );
            using Problem second = await Problem.ReadAsync( unknownLogin );
            Assert.Equal( "Неверный логин или пароль.", first.Body.GetProperty( "detail" ).GetString() );
            Assert.Equal( first.Body.GetProperty( "detail" ).GetString(), second.Body.GetProperty( "detail" ).GetString() );
            Assert.Equal( first.Title, second.Title );
        }

        [Fact]
        public async Task LockedAccountGets429WithWaitTime()
        {
            TestUser user = await TestUsers.CreateAsync( factory.Services );
            HttpClient client = factory.CreateBrowser();

            for ( int attempt = 1; attempt < IdentitySetup.MaxFailedAttempts; attempt++ )
            {
                HttpResponseMessage wrong = await client.LoginAsync( user.Login, "Wrong-password-1" );
                Assert.Equal( HttpStatusCode.Unauthorized, wrong.StatusCode );
            }

            HttpResponseMessage fifth = await client.LoginAsync( user.Login, "Wrong-password-1" );
            Assert.Equal( HttpStatusCode.TooManyRequests, fifth.StatusCode );

            // Даже верный пароль не пускает, пока блокировка не кончилась.
            HttpResponseMessage correct = await client.LoginAsync( user.Login, user.Password );
            Assert.Equal( HttpStatusCode.TooManyRequests, correct.StatusCode );
            int retryAfter = int.Parse( Assert.Single( correct.Headers.GetValues( "Retry-After" ) ), CultureInfo.InvariantCulture );
            Assert.InRange( retryAfter, 240, 300 );
            using Problem problem = await Problem.ReadAsync( correct );
            Assert.Equal( "Слишком много попыток входа", problem.Title );
            Assert.Equal( retryAfter, problem.Body.GetProperty( "retryAfterSeconds" ).GetInt32() );
        }

        [Fact]
        public async Task UnknownLoginIsAlsoLockedAfterFiveAttempts()
        {
            HttpClient client = factory.CreateBrowser();
            string login = $"ghost.{Guid.NewGuid():N}"[ ..20 ];

            for ( int attempt = 1; attempt < IdentitySetup.MaxFailedAttempts; attempt++ )
            {
                HttpResponseMessage wrong = await client.LoginAsync( login, "Wrong-password-1" );
                Assert.Equal( HttpStatusCode.Unauthorized, wrong.StatusCode );
            }

            HttpResponseMessage fifth = await client.LoginAsync( login, "Wrong-password-1" );

            // Так же, как у настоящей учётной записи: по ответу не понять, существует ли логин.
            Assert.Equal( HttpStatusCode.TooManyRequests, fifth.StatusCode );
        }

        [Fact]
        public async Task TooManyRequestsFromOneAddressGet429()
        {
            await using WebApplicationFactory<Program> limited = factory.WithWebHostBuilder( builder =>
                builder.UseSetting( $"{AuthRateLimitOptions.Section}:PermitPerMinute", "3" ) );
            HttpClient client = limited.CreateBrowser();

            for ( int request = 1; request <= 3; request++ )
            {
                HttpResponseMessage allowed = await client.LoginAsync( "someone", "Wrong-password-1" );
                Assert.NotEqual( HttpStatusCode.TooManyRequests, allowed.StatusCode );
            }

            HttpResponseMessage rejected = await client.LoginAsync( "someone", "Wrong-password-1" );

            Assert.Equal( HttpStatusCode.TooManyRequests, rejected.StatusCode );
            Assert.True( rejected.Headers.Contains( "Retry-After" ) );
            using Problem problem = await Problem.ReadAsync( rejected );
            Assert.Equal( "Слишком много попыток входа", problem.Title );
        }

        [Fact]
        public async Task EmptyLoginFormGets400WithFields()
        {
            HttpClient client = factory.CreateBrowser();

            HttpResponseMessage response = await client.PostAsJsonAsync( "/api/auth/login", new { login = "", password = "" }, Token );

            Assert.Equal( HttpStatusCode.BadRequest, response.StatusCode );
            using Problem problem = await Problem.ReadAsync( response );
            JsonElement errors = problem.Body.GetProperty( "errors" );
            Assert.Equal( "Введите логин", errors.GetProperty( "login" )[ 0 ].GetString() );
            Assert.Equal( "Введите пароль", errors.GetProperty( "password" )[ 0 ].GetString() );
        }

        [Fact]
        public async Task SessionSurvivesApiRestart()
        {
            TestUser user = await TestUsers.CreateAsync( factory.Services );
            HttpClient client = factory.CreateBrowser();
            HttpResponseMessage login = await client.LoginAsync( user.Login, user.Password );
            string cookie = login.Headers.GetValues( "Set-Cookie" ).First( value => value.StartsWith( SessionSetup.CookieName, StringComparison.Ordinal ) );
            string cookiePair = cookie.Split( ';' )[ 0 ];

            // Новый экземпляр API с той же базой: ключи подписи cookie он берёт из таблицы data_protection_keys.
            await using WebApplicationFactory<Program> restarted = factory.WithWebHostBuilder( _ => { } );
            HttpClient afterRestart = restarted.CreateClient( new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri( "https://localhost" ),
                HandleCookies = false,
            } );
            using HttpRequestMessage request = new( HttpMethod.Get, "/api/me" );
            request.Headers.Add( "Cookie", cookiePair );

            HttpResponseMessage me = await afterRestart.SendAsync( request, Token );

            Assert.Equal( HttpStatusCode.OK, me.StatusCode );
            Assert.True( await CountDataProtectionKeysAsync() > 0 );
        }

        [Fact]
        public async Task HealthStaysAvailableWithoutLogin()
        {
            HttpClient client = factory.CreateBrowser();

            HttpResponseMessage response = await client.GetAsync( "/api/health", Token );

            Assert.Equal( HttpStatusCode.OK, response.StatusCode );
        }

        private static readonly JsonSerializerOptions JsonOptions = new( JsonSerializerDefaults.Web )
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };

        private static DateTimeOffset ReadExpires( string setCookie )
        {
            string expires = setCookie.Split( ';' )
                .Select( part => part.Trim() )
                .First( part => part.StartsWith( "expires=", StringComparison.OrdinalIgnoreCase ) )[ "expires=".Length.. ];
            return DateTimeOffset.Parse( expires, CultureInfo.InvariantCulture );
        }

        private async Task<long> CountDataProtectionKeysAsync()
        {
            await using NpgsqlConnection connection = new( factory.ConnectionString );
            await connection.OpenAsync( Token );
            await using NpgsqlCommand command = new( "SELECT count(*) FROM data_protection_keys", connection );
            return ( long )( await command.ExecuteScalarAsync( Token ) )!;
        }
    }
}
