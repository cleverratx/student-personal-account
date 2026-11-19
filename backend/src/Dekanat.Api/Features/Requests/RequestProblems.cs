namespace Dekanat.Api.Features.Requests
{
    /// <summary>
    /// Как результат смены статуса превращается в ответ API: недопустимый переход это 409, неполные данные это 400.
    /// Эндпоинты студента и сотрудника используют это одинаково и текстов ошибок у себя не держат.
    /// </summary>
    public static class RequestProblems
    {
        /// <summary>Ответ с ошибкой для неудачного результата. Для успешного возвращает null.</summary>
        public static IResult? ToProblem( this TransitionResult result )
        {
            return result switch
            {
                TransitionResult.NotAllowed notAllowed => TypedResults.Problem(
                    title: "Статус заявки сейчас нельзя изменить",
                    detail: DescribeConflict( notAllowed ),
                    statusCode: StatusCodes.Status409Conflict ),
                TransitionResult.Invalid invalid => TypedResults.ValidationProblem( invalid.Errors ),
                _ => null,
            };
        }

        private static string DescribeConflict( TransitionResult.NotAllowed conflict )
        {
            if ( conflict.To == RequestStatus.Cancelled )
            {
                return "Отменить заявку можно только пока она в статусе «Подана». Дальше её ведёт деканат.";
            }

            if ( RequestWorkflow.IsFinal( conflict.From ) )
            {
                return $"Заявка уже в статусе «{RequestWorkflow.Title( conflict.From )}», больше её статус не меняется.";
            }

            return $"Заявку в статусе «{RequestWorkflow.Title( conflict.From )}» нельзя перевести в статус «{RequestWorkflow.Title( conflict.To )}».";
        }
    }
}
