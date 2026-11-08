using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Dekanat.Api.Infrastructure.Errors
{
    /// <summary>
    /// Последний шаг перед отправкой любого ProblemDetails: русские тексты и requestId.
    /// </summary>
    public static class ProblemDetailsCustomizer
    {
        public const string FallbackFieldError = "Проверьте значение этого поля";

        public static void Customize( ProblemDetailsContext context )
        {
            ProblemDetails problem = context.ProblemDetails;
            int status = problem.Status ?? context.HttpContext.Response.StatusCode;
            bool isValidation = problem is HttpValidationProblemDetails;

            // Заголовок, который задал раздел ("Заявка не найдена"), оставляем.
            // Английский заголовок по умолчанию ("Not Found") заменяем своим.
            if ( !IsRussian( problem.Title ) )
            {
                problem.Title = TitleFor( status, isValidation );
            }

            if ( problem is HttpValidationProblemDetails validation )
            {
                NormalizeValidationErrors( validation.Errors );
            }

            // Один идентификатор запроса и в ответе, и в заголовке X-Request-Id, и в логах.
            problem.Extensions.Remove( "traceId" );
            problem.Extensions[ "requestId" ] = context.HttpContext.TraceIdentifier;
        }

        public static string TitleFor( int status, bool isValidation = false )
        {
            return status switch
            {
                StatusCodes.Status400BadRequest when isValidation => "Проверьте введённые данные",
                StatusCodes.Status400BadRequest => "Некорректный запрос",
                StatusCodes.Status401Unauthorized => "Нужно войти в кабинет",
                StatusCodes.Status403Forbidden => "Недостаточно прав",
                StatusCodes.Status404NotFound => "Не найдено",
                StatusCodes.Status405MethodNotAllowed => "Такой метод здесь не поддерживается",
                StatusCodes.Status409Conflict => "Действие сейчас невозможно",
                StatusCodes.Status413PayloadTooLarge => "Слишком большой запрос",
                StatusCodes.Status415UnsupportedMediaType => "Неподдерживаемый формат данных",
                StatusCodes.Status429TooManyRequests => "Слишком много запросов",
                StatusCodes.Status500InternalServerError => "Внутренняя ошибка сервера",
                StatusCodes.Status503ServiceUnavailable => "Сервис временно недоступен",
                >= 500 => "Ошибка сервера",
                _ => "Ошибка запроса",
            };
        }

        /// <summary>
        /// Ключи ошибок называем как поля в JSON запроса (login, items[0].name), чтобы фронт сразу нашёл поле формы.
        /// Английский текст по умолчанию заменяем на русский: это страховка, если у атрибута забыли ErrorMessage.
        /// </summary>
        private static void NormalizeValidationErrors( IDictionary<string, string[]> errors )
        {
            KeyValuePair<string, string[]>[] fields = [ .. errors ];
            errors.Clear();

            foreach ( (string field, string[] messages) in fields )
            {
                string key = string.Join( '.', field.Split( '.' ).Select( JsonNamingPolicy.CamelCase.ConvertName ) );
                string[] russian = [ .. messages.Select( message => IsRussian( message ) ? message : FallbackFieldError ).Distinct() ];
                errors[ key ] = errors.TryGetValue( key, out string[]? existing ) ? [ .. existing, .. russian ] : russian;
            }
        }

        private static bool IsRussian( string? text )
        {
            return text is not null && text.Any( c => c is ( >= 'А' and <= 'я' ) or 'ё' or 'Ё' );
        }
    }
}
