namespace Dekanat.Api.Infrastructure.Errors
{
    /// <summary>
    /// Единый формат ошибок: любой ответ 4xx и 5xx приходит как ProblemDetails с заголовком по-русски
    /// и с requestId, тем же, что в заголовке X-Request-Id и в логах сервера.
    /// </summary>
    public static class ErrorHandlingSetup
    {
        public const string RequestIdHeader = "X-Request-Id";

        public static IServiceCollection AddErrorHandling( this IServiceCollection services )
        {
            services.AddProblemDetails( options => options.CustomizeProblemDetails = ProblemDetailsCustomizer.Customize );
            services.AddExceptionHandler<UnhandledExceptionHandler>();

            // Атрибуты DataAnnotations у параметров и DTO эндпоинтов проверяются до вызова обработчика,
            // при ошибке приходит 400 со списком полей. Текст ошибки пишем в атрибуте по-русски:
            // [Required( ErrorMessage = "Введите логин" )].
            services.AddValidation();

            // Ошибки чтения запроса (битый JSON, не тот Content-Type) всегда бросаем исключением. Тогда их
            // разбирает UnhandledExceptionHandler, и ответ одинаковый и в Development, и в Production.
            services.Configure<RouteHandlerOptions>( options => options.ThrowOnBadRequest = true );

            // В логах запроса остаётся RequestId из X-Request-Id, а TraceId и SpanId нам пока не нужны.
            services.Configure<LoggerFactoryOptions>( options => options.ActivityTrackingOptions = ActivityTrackingOptions.None );

            return services;
        }

        /// <summary>
        /// Ставится первым в конвейере, чтобы ошибки всех следующих шагов приходили как ProblemDetails.
        /// </summary>
        public static IApplicationBuilder UseErrorHandling( this IApplicationBuilder app )
        {
            app.Use( ( context, next ) =>
            {
                // Через OnStarting, а не сразу: перед ответом 500 обработчик исключений очищает заголовки.
                context.Response.OnStarting( () =>
                {
                    context.Response.Headers[ RequestIdHeader ] = context.TraceIdentifier;
                    return Task.CompletedTask;
                } );
                return next( context );
            } );

            app.UseExceptionHandler();

            // Ответы без тела (404 на неизвестный адрес, 405, позже 401 и 403) тоже получают ProblemDetails.
            app.UseStatusCodePages();

            return app;
        }

        /// <summary>
        /// Описывает в OpenAPI ответ 500 в формате ProblemDetails для всех эндпоинтов группы.
        /// </summary>
        public static RouteGroupBuilder WithProblemResponses( this RouteGroupBuilder group )
        {
            return group.ProducesProblem( StatusCodes.Status500InternalServerError );
        }
    }
}
