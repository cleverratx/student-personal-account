using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Dekanat.Api.Infrastructure.Errors
{
    /// <summary>
    /// Превращает исключение в ProblemDetails. Наружу уходит только общий текст, стектрейс и сообщение
    /// исключения остаются в логе сервера.
    /// </summary>
    public sealed class UnhandledExceptionHandler(
        IProblemDetailsService problemDetails,
        ILogger<UnhandledExceptionHandler> logger ) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync( HttpContext httpContext, Exception exception, CancellationToken cancellationToken )
        {
            ProblemDetails problem;

            if ( exception is BadHttpRequestException badRequest )
            {
                // Битый JSON, не тот Content-Type, слишком большое тело. Это ошибка клиента, а не сервера.
                logger.LogInformation( "Не удалось прочитать запрос: {Reason}", badRequest.Message );
                problem = new ProblemDetails
                {
                    Status = badRequest.StatusCode,
                    Detail = "Не удалось прочитать запрос. Проверьте, что данные отправлены в формате JSON.",
                };
            }
            else
            {
                // С .NET 10 middleware сам не пишет в лог исключения, которые обработал IExceptionHandler.
                logger.LogError( exception, "Необработанное исключение при обработке запроса" );
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = "Что-то пошло не так на нашей стороне. Попробуйте ещё раз чуть позже.",
                };
            }

            httpContext.Response.StatusCode = problem.Status.Value;
            return await problemDetails.TryWriteAsync( new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problem,
            } );
        }
    }
}
