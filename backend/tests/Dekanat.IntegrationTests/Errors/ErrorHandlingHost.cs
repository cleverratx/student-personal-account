using System.ComponentModel.DataAnnotations;
using Dekanat.Api.Infrastructure.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dekanat.IntegrationTests.Errors
{
    /// <summary>
    /// Маленькое приложение с той же обработкой ошибок, что в Program.cs (AddErrorHandling и UseErrorHandling),
    /// и с тестовыми эндпоинтами: в настоящем API пока нет эндпоинтов, которые принимают данные или падают.
    /// </summary>
    public sealed class ErrorHandlingHost : IAsyncLifetime
    {
        public const string SecretMessage = "секретная деталь из исключения: пароль 12345";

        private WebApplication? _app;

        public LogCollector Logs { get; } = new();

        public HttpClient CreateClient()
        {
            return _app!.GetTestClient();
        }

        public async ValueTask InitializeAsync()
        {
            WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider( Logs );

            builder.Services.AddErrorHandling();
            // Отдельный вызов в тестовой сборке, чтобы генератор валидации описал и SampleRequest.
            builder.Services.AddValidation();

            _app = builder.Build();
            _app.UseErrorHandling();

            _app.MapPost( "/sample", ( SampleRequest request ) => TypedResults.NoContent() );
            _app.MapGet( "/throw", ThrowSecret );
            _app.MapGet( "/status/{code:int}", ( int code ) => Results.StatusCode( code ) );
            _app.MapGet( "/request-not-found", () => TypedResults.Problem(
                title: "Заявка не найдена",
                detail: "Заявка не найдена или недоступна.",
                statusCode: StatusCodes.Status404NotFound ) );

            await _app.StartAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if ( _app is not null )
            {
                await _app.DisposeAsync();
            }
        }

        private static IResult ThrowSecret()
        {
            throw new InvalidOperationException( SecretMessage );
        }
    }

    public sealed class SampleRequest
    {
        [Required( ErrorMessage = "Введите логин" )]
        public string? Login { get; init; }

        [Range( 1, 10, ErrorMessage = "Укажите число от 1 до 10" )]
        public int Count { get; init; }

        // Без ErrorMessage: проверяем, что английский текст по умолчанию не уйдёт наружу.
        [EmailAddress]
        public string? Email { get; init; }
    }
}
