using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Dekanat.IntegrationTests.Errors
{
    public sealed class ErrorHandlingTests( ErrorHandlingHost host ) : IClassFixture<ErrorHandlingHost>
    {
        private readonly HttpClient _client = host.CreateClient();

        [Fact]
        public async Task InvalidRequestReturns400WithRussianMessagePerField()
        {
            HttpResponseMessage response = await _client.PostAsJsonAsync(
                "/sample",
                new { count = 20, email = "не почта" },
                TestContext.Current.CancellationToken );

            Assert.Equal( HttpStatusCode.BadRequest, response.StatusCode );
            using Problem problem = await Problem.ReadAsync( response );
            Assert.Equal( "application/problem+json", problem.ContentType );
            Assert.Equal( "Проверьте введённые данные", problem.Title );

            JsonElement errors = problem.Body.GetProperty( "errors" );
            Assert.Equal( [ "Введите логин" ], ReadMessages( errors, "login" ) );
            Assert.Equal( [ "Укажите число от 1 до 10" ], ReadMessages( errors, "count" ) );
            Assert.Equal( [ "Проверьте значение этого поля" ], ReadMessages( errors, "email" ) );
        }

        [Fact]
        public async Task ValidRequestPassesValidation()
        {
            HttpResponseMessage response = await _client.PostAsJsonAsync(
                "/sample",
                new { login = "student", count = 3, email = "student@example.ru" },
                TestContext.Current.CancellationToken );

            Assert.Equal( HttpStatusCode.NoContent, response.StatusCode );
        }

        [Fact]
        public async Task BrokenJsonReturns400WithoutParserDetails()
        {
            using StringContent body = new( "{ \"login\": ", Encoding.UTF8, "application/json" );

            HttpResponseMessage response = await _client.PostAsync( "/sample", body, TestContext.Current.CancellationToken );

            Assert.Equal( HttpStatusCode.BadRequest, response.StatusCode );
            using Problem problem = await Problem.ReadAsync( response );
            Assert.Equal( "Некорректный запрос", problem.Title );
            Assert.Equal(
                "Не удалось прочитать запрос. Проверьте, что данные отправлены в формате JSON.",
                problem.Body.GetProperty( "detail" ).GetString() );
            Assert.DoesNotContain( "Json", problem.RawBody, StringComparison.Ordinal );
        }

        [Fact]
        public async Task ExceptionReturns500WithoutDetails()
        {
            HttpResponseMessage response = await _client.GetAsync( "/throw", TestContext.Current.CancellationToken );

            Assert.Equal( HttpStatusCode.InternalServerError, response.StatusCode );
            using Problem problem = await Problem.ReadAsync( response );
            Assert.Equal( "application/problem+json", problem.ContentType );
            Assert.Equal( "Внутренняя ошибка сервера", problem.Title );
            Assert.Equal( 500, problem.Status );

            // Наружу ни текста исключения, ни его типа, ни стектрейса.
            Assert.DoesNotContain( "секретная", problem.RawBody, StringComparison.Ordinal );
            Assert.DoesNotContain( "InvalidOperationException", problem.RawBody, StringComparison.Ordinal );
            Assert.DoesNotContain( "ErrorHandlingHost", problem.RawBody, StringComparison.Ordinal );
            Assert.False( problem.Body.TryGetProperty( "exception", out _ ) );
        }

        [Fact]
        public async Task ExceptionIsLoggedWithSameRequestIdAsResponse()
        {
            HttpResponseMessage response = await _client.GetAsync( "/throw", TestContext.Current.CancellationToken );
            using Problem problem = await Problem.ReadAsync( response );

            Assert.False( string.IsNullOrEmpty( problem.RequestIdHeader ) );
            Assert.Equal( problem.RequestIdHeader, problem.RequestId );

            LogEntry entry = Assert.Single(
                host.Logs.Entries,
                log => log.Level == LogLevel.Error
                    && log.Scope.TryGetValue( "RequestId", out object? requestId )
                    && Equals( requestId, problem.RequestIdHeader ) );
            Assert.Equal( ErrorHandlingHost.SecretMessage, entry.Exception?.Message );
        }

        [Theory]
        [InlineData( 401, "Нужно войти в кабинет" )]
        [InlineData( 403, "Недостаточно прав" )]
        [InlineData( 404, "Не найдено" )]
        [InlineData( 409, "Действие сейчас невозможно" )]
        public async Task StatusWithoutBodyBecomesRussianProblemDetails( int status, string title )
        {
            HttpResponseMessage response = await _client.GetAsync( $"/status/{status}", TestContext.Current.CancellationToken );

            Assert.Equal( status, ( int )response.StatusCode );
            using Problem problem = await Problem.ReadAsync( response );
            Assert.Equal( "application/problem+json", problem.ContentType );
            Assert.Equal( title, problem.Title );
            Assert.Equal( status, problem.Status );
            Assert.Equal( problem.RequestIdHeader, problem.RequestId );
        }

        [Fact]
        public async Task TitleFromEndpointIsKept()
        {
            HttpResponseMessage response = await _client.GetAsync( "/request-not-found", TestContext.Current.CancellationToken );

            Assert.Equal( HttpStatusCode.NotFound, response.StatusCode );
            using Problem problem = await Problem.ReadAsync( response );
            Assert.Equal( "Заявка не найдена", problem.Title );
            Assert.Equal( "Заявка не найдена или недоступна.", problem.Body.GetProperty( "detail" ).GetString() );
        }

        private static string?[] ReadMessages( JsonElement errors, string field )
        {
            return errors.GetProperty( field ).EnumerateArray().Select( message => message.GetString() ).ToArray();
        }
    }
}
