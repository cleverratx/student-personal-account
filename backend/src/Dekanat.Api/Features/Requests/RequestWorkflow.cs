namespace Dekanat.Api.Features.Requests
{
    /// <summary>Кто меняет статус. От этого зависит, какие переходы разрешены.</summary>
    public enum RequestActor
    {
        Student,
        Staff,
    }

    /// <param name="To">Новый статус.</param>
    /// <param name="Actor">Кто меняет: студент или сотрудник.</param>
    /// <param name="ChangedByUserId">Учётная запись автора, попадает в историю.</param>
    /// <param name="Comment">Комментарий. Для отказа обязателен: это причина отказа.</param>
    /// <param name="IssuePlace">Где забрать. Обязательно при переходе в «Готова».</param>
    /// <param name="IssueDeadline">До какого времени забрать, UTC. Обязательно при переходе в «Готова».</param>
    public sealed record StatusChange(
        RequestStatus To,
        RequestActor Actor,
        Guid ChangedByUserId,
        string? Comment = null,
        string? IssuePlace = null,
        DateTime? IssueDeadline = null );

    /// <summary>Итог попытки сменить статус. Без исключений: эндпоинт сам решает, что вернуть (см. <see cref="RequestProblems"/>).</summary>
    public abstract record TransitionResult
    {
        /// <summary>Статус сменился, заявка изменена, запись истории добавлена в <see cref="Request.History"/>.</summary>
        public sealed record Success( RequestStatusHistory Entry ) : TransitionResult;

        /// <summary>Такого перехода нет в таблице. Заявка не тронута. Это 409.</summary>
        public sealed record NotAllowed( RequestStatus From, RequestStatus To ) : TransitionResult;

        /// <summary>Переход допустим, но данные неполные (нет причины отказа, места выдачи). Заявка не тронута. Это 400.</summary>
        public sealed record Invalid( Dictionary<string, string[]> Errors ) : TransitionResult;
    }

    /// <summary>
    /// Жизненный цикл заявки. Все правила смены статуса живут здесь и больше нигде: таблица переходов,
    /// кто какой переход делает, какие данные нужны, запись истории и версия для блокировки.
    /// Эндпоинты студента и сотрудника только вызывают <see cref="Apply"/>.
    /// </summary>
    public static class RequestWorkflow
    {
        public const int MaxCommentLength = 500;

        public const int MaxIssuePlaceLength = 200;

        /// <summary>
        /// Допустимые переходы и кто их делает. Подана → В работе → Готова → Выдана, отказать можно, пока заявку
        /// не изготовили, отменить может только студент и только поданную. Выдана, Отклонена и Отменена конечные.
        /// </summary>
        private static readonly Dictionary<(RequestStatus From, RequestStatus To), RequestActor> Transitions = new()
        {
            [ (RequestStatus.Submitted, RequestStatus.InProgress) ] = RequestActor.Staff,
            [ (RequestStatus.Submitted, RequestStatus.Rejected) ] = RequestActor.Staff,
            [ (RequestStatus.Submitted, RequestStatus.Cancelled) ] = RequestActor.Student,
            [ (RequestStatus.InProgress, RequestStatus.Ready) ] = RequestActor.Staff,
            [ (RequestStatus.InProgress, RequestStatus.Rejected) ] = RequestActor.Staff,
            [ (RequestStatus.Ready, RequestStatus.Issued) ] = RequestActor.Staff,
        };

        public static bool IsAllowed( RequestStatus from, RequestStatus to, RequestActor actor )
        {
            return Transitions.TryGetValue( (from, to), out RequestActor allowed ) && allowed == actor;
        }

        /// <summary>Куда этот человек может перевести заявку сейчас. Из этого фронт строит кнопки.</summary>
        public static IReadOnlyList<RequestStatus> NextStatuses( RequestStatus from, RequestActor actor )
        {
            return [ .. Transitions.Where( item => item.Key.From == from && item.Value == actor ).Select( item => item.Key.To ) ];
        }

        public static bool IsFinal( RequestStatus status )
        {
            return Transitions.Keys.All( key => key.From != status );
        }

        /// <summary>
        /// Создаёт поданную заявку: статус «Подана», первая запись истории и примерная дата готовности.
        /// </summary>
        /// <param name="submittedOn">Сегодняшняя дата по времени вуза, от неё считаются рабочие дни.</param>
        public static Request Submit(
            Guid studentProfileId,
            Guid studentUserId,
            RequestType type,
            string fields,
            DateTime now,
            DateOnly submittedOn )
        {
            Request request = new()
            {
                Id = Guid.NewGuid(),
                StudentProfileId = studentProfileId,
                RequestTypeId = type.Id,
                Fields = fields,
                CreatedAt = now,
                CurrentStatus = RequestStatus.Submitted,
                EstimatedReadyDate = EstimateReadyDate( submittedOn, type.ProcessingDays ),
            };
            request.History.Add( new RequestStatusHistory
            {
                Id = Guid.NewGuid(),
                RequestId = request.Id,
                Status = RequestStatus.Submitted,
                ChangedAt = now,
                ChangedByUserId = studentUserId,
            } );
            return request;
        }

        /// <summary>
        /// Дата через указанное число рабочих дней. Считаем только субботы и воскресенья: календаря праздников у нас нет,
        /// поэтому дата примерная.
        /// </summary>
        public static DateOnly EstimateReadyDate( DateOnly from, int workingDays )
        {
            DateOnly date = from;
            while ( workingDays > 0 )
            {
                date = date.AddDays( 1 );
                if ( date.DayOfWeek is not ( DayOfWeek.Saturday or DayOfWeek.Sunday ) )
                {
                    workingDays--;
                }
            }

            return date;
        }

        /// <summary>
        /// Меняет статус заявки. При успехе обновляет статус, данные выдачи и версию и добавляет запись в историю.
        /// При любой ошибке заявку не меняет. Сначала проверяется сам переход (409), потом данные (400).
        /// </summary>
        public static TransitionResult Apply( Request request, StatusChange change, DateTime now )
        {
            if ( !IsAllowed( request.CurrentStatus, change.To, change.Actor ) )
            {
                return new TransitionResult.NotAllowed( request.CurrentStatus, change.To );
            }

            string? comment = string.IsNullOrWhiteSpace( change.Comment ) ? null : change.Comment.Trim();
            string? issuePlace = string.IsNullOrWhiteSpace( change.IssuePlace ) ? null : change.IssuePlace.Trim();

            Dictionary<string, string[]> errors = Validate( change, comment, issuePlace, now );
            if ( errors.Count > 0 )
            {
                return new TransitionResult.Invalid( errors );
            }

            request.CurrentStatus = change.To;
            request.Version++;
            if ( change.To == RequestStatus.Ready )
            {
                request.IssuePlace = issuePlace;
                request.IssueDeadline = change.IssueDeadline;
            }

            RequestStatusHistory entry = new()
            {
                Id = Guid.NewGuid(),
                RequestId = request.Id,
                Status = change.To,
                Comment = comment,
                ChangedAt = now,
                ChangedByUserId = change.ChangedByUserId,
            };
            request.History.Add( entry );
            return new TransitionResult.Success( entry );
        }

        /// <summary>Название статуса для людей, как в интерфейсе.</summary>
        public static string Title( RequestStatus status )
        {
            return status switch
            {
                RequestStatus.Submitted => "Подана",
                RequestStatus.InProgress => "В работе",
                RequestStatus.Ready => "Готова",
                RequestStatus.Issued => "Выдана",
                RequestStatus.Rejected => "Отклонена",
                RequestStatus.Cancelled => "Отменена",
                _ => status.ToString(),
            };
        }

        private static Dictionary<string, string[]> Validate( StatusChange change, string? comment, string? issuePlace, DateTime now )
        {
            Dictionary<string, string[]> errors = [];

            if ( comment is { Length: > MaxCommentLength } )
            {
                errors[ "comment" ] = [ $"Комментарий не длиннее {MaxCommentLength} символов" ];
            }

            if ( change.To == RequestStatus.Rejected && comment is null )
            {
                errors[ "comment" ] = [ "Укажите причину отказа" ];
            }

            if ( change.To == RequestStatus.Ready )
            {
                if ( issuePlace is null )
                {
                    errors[ "issuePlace" ] = [ "Укажите, где забрать документ" ];
                }
                else if ( issuePlace.Length > MaxIssuePlaceLength )
                {
                    errors[ "issuePlace" ] = [ $"Место выдачи не длиннее {MaxIssuePlaceLength} символов" ];
                }

                if ( change.IssueDeadline is null )
                {
                    errors[ "issueDeadline" ] = [ "Укажите, до какого времени забрать документ" ];
                }
                else if ( change.IssueDeadline <= now )
                {
                    errors[ "issueDeadline" ] = [ "Срок выдачи должен быть в будущем" ];
                }
            }

            return errors;
        }
    }
}
