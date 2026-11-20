using System.Security.Claims;
using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Profile;
using Dekanat.Api.Infrastructure.Persistence;
using Dekanat.Api.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace Dekanat.Api.Features.Schedule
{
    /// <param name="Type">Что случилось: перенос, другая аудитория или отмена.</param>
    /// <param name="OldStartsAt">Когда пара была до переноса, UTC. Есть только у переноса.</param>
    /// <param name="NewStartsAt">Когда пара теперь, UTC. Есть только у переноса.</param>
    /// <param name="OldRoom">Аудитория до изменения.</param>
    /// <param name="NewRoom">Аудитория теперь.</param>
    /// <param name="Reason">Причина, как её написал деканат.</param>
    /// <param name="ChangedAt">Когда деканат внёс изменение, UTC.</param>
    public sealed record LessonChangeDto(
        LessonChangeType Type,
        DateTime? OldStartsAt,
        DateTime? NewStartsAt,
        string? OldRoom,
        string? NewRoom,
        string? Reason,
        DateTime ChangedAt );

    /// <param name="Id">Id пары.</param>
    /// <param name="Discipline">Дисциплина.</param>
    /// <param name="Type">Тип занятия.</param>
    /// <param name="StartsAt">Начало, UTC, уже с учётом переноса.</param>
    /// <param name="EndsAt">Конец, UTC.</param>
    /// <param name="Room">Аудитория сейчас.</param>
    /// <param name="Teacher">Преподаватель: фамилия и инициалы.</param>
    /// <param name="IsCancelled">Пара отменена.</param>
    /// <param name="Change">Что поменялось относительно исходного расписания. Пусто, если ничего.</param>
    public sealed record LessonDto(
        Guid Id,
        string Discipline,
        LessonType Type,
        DateTime StartsAt,
        DateTime EndsAt,
        string Room,
        string Teacher,
        bool IsCancelled,
        LessonChangeDto? Change );

    /// <param name="From">Первый день периода по времени вуза.</param>
    /// <param name="To">Последний день периода включительно.</param>
    /// <param name="TimeZone">Часовой пояс вуза (IANA): в нём фронт показывает время и делит пары по дням.</param>
    /// <param name="Group">Группа студента.</param>
    /// <param name="Items">Пары по возрастанию времени, отменённые тоже.</param>
    public sealed record ScheduleResponse( DateOnly From, DateOnly To, string TimeZone, string Group, IReadOnlyList<LessonDto> Items );

    public static class ScheduleEndpoints
    {
        /// <summary>Самый длинный период за один запрос: хватает на месяц с запасом.</summary>
        public const int MaxDays = 62;

        public static RouteGroupBuilder MapSchedule( this RouteGroupBuilder api )
        {
            api.MapGet( "/schedule", GetScheduleAsync )
                .WithTags( "Schedule" )
                .WithName( "GetSchedule" )
                .WithSummary( "Пары группы того, кто вошёл, за период. Группу сервер берёт только из сессии" )
                .RequireAuthorization( AuthorizationPolicies.Student )
                .Produces<ScheduleResponse>()
                .ProducesValidationProblem()
                .ProducesProblem( StatusCodes.Status401Unauthorized )
                .ProducesProblem( StatusCodes.Status403Forbidden )
                .ProducesProblem( StatusCodes.Status404NotFound );

            return api;
        }

        /// <param name="from">Первый день, ГГГГ-ММ-ДД по времени вуза.</param>
        /// <param name="to">Последний день включительно, не дальше 62 дней от первого.</param>
        private static async Task<IResult> GetScheduleAsync(
            DateOnly? from,
            DateOnly? to,
            ClaimsPrincipal principal,
            AppDbContext db,
            UniversityClock clock,
            CancellationToken cancellationToken )
        {
            Dictionary<string, string[]> errors = ValidatePeriod( from, to );
            if ( errors.Count > 0 )
            {
                return TypedResults.ValidationProblem( errors );
            }

            // Группу берём из профиля того, кто вошёл. Параметров с id группы или студента нет.
            Guid userId = Guid.Parse( principal.FindFirstValue( ClaimTypes.NameIdentifier )! );
            var student = await db.Set<StudentProfile>()
                .Where( profile => profile.UserId == userId )
                .Select( profile => new { profile.GroupId, Group = profile.Group.Name } )
                .SingleOrDefaultAsync( cancellationToken );
            if ( student is null )
            {
                return TypedResults.Problem(
                    title: "Профиль не найден",
                    detail: "Для этой учётной записи профиль студента ещё не заведён. Обратитесь в деканат.",
                    statusCode: StatusCodes.Status404NotFound );
            }

            DateTime fromUtc = clock.ToUtc( from!.Value, TimeOnly.MinValue );
            DateTime toUtc = clock.ToUtc( to!.Value.AddDays( 1 ), TimeOnly.MinValue );

            var lessons = await db.Set<Lesson>()
                .Where( lesson => lesson.GroupId == student.GroupId && lesson.StartsAt >= fromUtc && lesson.StartsAt < toUtc )
                .OrderBy( lesson => lesson.StartsAt )
                .Select( lesson => new
                {
                    lesson.Id,
                    Discipline = lesson.Discipline.Name,
                    lesson.Type,
                    lesson.StartsAt,
                    lesson.EndsAt,
                    lesson.Room,
                    Teacher = lesson.Teacher.FullName,
                    lesson.IsCancelled,
                    Changes = lesson.Changes.OrderBy( change => change.ChangedAt ).ToList(),
                } )
                .AsNoTracking()
                .ToListAsync( cancellationToken );

            LessonDto[] items =
            [
                .. lessons.Select( lesson => new LessonDto(
                    lesson.Id,
                    lesson.Discipline,
                    lesson.Type,
                    lesson.StartsAt,
                    lesson.EndsAt,
                    lesson.Room,
                    ShortName( lesson.Teacher ),
                    lesson.IsCancelled,
                    SummarizeChanges( lesson.Changes, lesson.StartsAt, lesson.Room, lesson.IsCancelled ) ) ),
            ];

            return TypedResults.Ok( new ScheduleResponse( from.Value, to.Value, clock.ZoneId, student.Group, items ) );
        }

        private static Dictionary<string, string[]> ValidatePeriod( DateOnly? from, DateOnly? to )
        {
            Dictionary<string, string[]> errors = [];
            if ( from is null )
            {
                errors[ "from" ] = [ "Укажите первый день периода в формате ГГГГ-ММ-ДД" ];
            }

            if ( to is null )
            {
                errors[ "to" ] = [ "Укажите последний день периода в формате ГГГГ-ММ-ДД" ];
            }

            if ( from is not null && to is not null )
            {
                if ( to < from )
                {
                    errors[ "to" ] = [ "Последний день не может быть раньше первого" ];
                }
                else if ( to.Value.DayNumber - from.Value.DayNumber >= MaxDays )
                {
                    errors[ "to" ] = [ $"За один раз можно получить не больше {MaxDays} дней" ];
                }
            }

            return errors;
        }

        /// <summary>
        /// Изменения пары одним итогом: что было в исходном расписании и что теперь. Если пару сначала перенесли,
        /// а потом ещё раз сменили аудиторию, студенту важно сравнение с исходным, а не с промежуточным вариантом.
        /// </summary>
        public static LessonChangeDto? SummarizeChanges( IReadOnlyList<LessonChange> changes, DateTime startsAt, string room, bool isCancelled )
        {
            if ( changes.Count == 0 )
            {
                return null;
            }

            LessonChange last = changes[ ^1 ];
            DateTime? oldStartsAt = changes.FirstOrDefault( change => change.OldStartsAt is not null )?.OldStartsAt;
            string? oldRoom = changes.FirstOrDefault( change => change.OldRoom is not null )?.OldRoom;

            // Вернули как было: студенту показывать нечего.
            bool moved = oldStartsAt is not null && oldStartsAt != startsAt;
            bool roomChanged = oldRoom is not null && oldRoom != room;

            LessonChangeType? type = isCancelled
                ? LessonChangeType.Cancellation
                : moved ? LessonChangeType.Reschedule : roomChanged ? LessonChangeType.RoomChange : null;

            return type is null
                ? null
                : new LessonChangeDto(
                    type.Value,
                    moved ? oldStartsAt : null,
                    moved ? startsAt : null,
                    roomChanged ? oldRoom : null,
                    roomChanged ? room : null,
                    last.Reason,
                    last.ChangedAt );
        }

        /// <summary>«Беляев Арсений Константинович» → «Беляев А. К.», как пишут в расписании.</summary>
        public static string ShortName( string fullName )
        {
            string[] parts = fullName.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
            return parts.Length switch
            {
                0 => "",
                1 => parts[ 0 ],
                _ => $"{parts[ 0 ]} {string.Join( " ", parts.Skip( 1 ).Select( part => $"{char.ToUpperInvariant( part[ 0 ] )}." ) )}",
            };
        }
    }
}
