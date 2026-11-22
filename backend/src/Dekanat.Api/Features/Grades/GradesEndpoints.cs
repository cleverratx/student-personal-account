using System.Security.Claims;
using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Profile;
using Dekanat.Api.Infrastructure.Persistence;
using Dekanat.Api.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace Dekanat.Api.Features.Grades
{
    // ──────────────────────────────── DTOs ────────────────────────────────

    /// <param name="DisciplineId">Id дисциплины.</param>
    /// <param name="DisciplineName">Название дисциплины.</param>
    /// <param name="ControlForm">Форма контроля: Exam, Credit, GradedCredit, CourseWork.</param>
    /// <param name="Grade">
    /// Оценка (5/4/3/2) для Exam и GradedCredit; null для Credit и CourseWork.
    /// </param>
    /// <param name="IsPassed">
    /// Зачтено (true/false) для Credit и CourseWork; null для Exam и GradedCredit.
    /// </param>
    /// <param name="Points">Баллы БРС, если применяется. Null, если не используется.</param>
    /// <param name="Date">Дата аттестации. Null, если ещё не проходила.</param>
    /// <param name="Status">Статус: Passed, Failed, NotAdmitted, Debt.</param>
    public sealed record AssessmentDto(
        Guid DisciplineId,
        string DisciplineName,
        ControlForm ControlForm,
        int? Grade,
        bool? IsPassed,
        int? Points,
        DateOnly? Date,
        AssessmentStatus Status );

    /// <param name="AverageGrade">
    /// Средний балл за семестр (только оценки 5/4/3 по Exam и GradedCredit).
    /// Null, если нет ни одной оценки.
    /// </param>
    /// <param name="DebtCount">Число дисциплин со статусом Debt.</param>
    /// <param name="RemainingCount">Число дисциплин, которые ещё не сданы (статус Failed или без даты).</param>
    public sealed record GradesSummaryDto(
        double? AverageGrade,
        int DebtCount,
        int RemainingCount );

    /// <param name="Semester">Семестр, за который возвращены оценки.</param>
    /// <param name="Summary">Сводка по семестру.</param>
    /// <param name="Items">Список дисциплин с результатами.</param>
    public sealed record GradesResponse(
        int Semester,
        GradesSummaryDto Summary,
        IReadOnlyList<AssessmentDto> Items );

    // ──────────────────────────────── Session ─────────────────────────────

    /// <param name="Id">Id события сессии.</param>
    /// <param name="DisciplineName">Дисциплина.</param>
    /// <param name="Type">Тип: Exam, Credit, GradedCredit, Consultation.</param>
    /// <param name="StartsAt">Начало, UTC.</param>
    /// <param name="EndsAt">Конец, UTC.</param>
    /// <param name="Room">Аудитория.</param>
    /// <param name="Teacher">Преподаватель: фамилия и инициалы.</param>
    public sealed record SessionEventDto(
        Guid Id,
        string DisciplineName,
        SessionEventType Type,
        DateTime StartsAt,
        DateTime EndsAt,
        string Room,
        string Teacher );

    /// <param name="Semester">Семестр сессии.</param>
    /// <param name="TimeZone">Часовой пояс вуза (IANA).</param>
    /// <param name="Items">События по возрастанию времени.</param>
    public sealed record SessionResponse(
        int Semester,
        string TimeZone,
        IReadOnlyList<SessionEventDto> Items );

    // ──────────────────────────────── Endpoints ───────────────────────────

    public static class GradesEndpoints
    {
        public static RouteGroupBuilder MapGrades( this RouteGroupBuilder api )
        {
            api.MapGet( "/grades", GetGradesAsync )
                .WithTags( "Grades" )
                .WithName( "GetGrades" )
                .WithSummary( "Успеваемость текущего студента за семестр. Студент берётся только из сессии" )
                .RequireAuthorization( AuthorizationPolicies.Student )
                .Produces<GradesResponse>()
                .ProducesValidationProblem()
                .ProducesProblem( StatusCodes.Status401Unauthorized )
                .ProducesProblem( StatusCodes.Status403Forbidden )
                .ProducesProblem( StatusCodes.Status404NotFound );

            api.MapGet( "/session", GetSessionAsync )
                .WithTags( "Grades" )
                .WithName( "GetSession" )
                .WithSummary( "Расписание сессии (экзамены, зачёты, консультации) для группы текущего студента" )
                .RequireAuthorization( AuthorizationPolicies.Student )
                .Produces<SessionResponse>()
                .ProducesValidationProblem()
                .ProducesProblem( StatusCodes.Status401Unauthorized )
                .ProducesProblem( StatusCodes.Status403Forbidden )
                .ProducesProblem( StatusCodes.Status404NotFound );

            return api;
        }

        // GET /api/grades?semester=N
        private static async Task<IResult> GetGradesAsync(
            int? semester,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken cancellationToken )
        {
            if ( semester is null or < 1 or > 20 )
            {
                return TypedResults.ValidationProblem( new Dictionary<string, string[]>
                {
                    [ "semester" ] = [ "Укажите номер семестра (целое число от 1 до 20)" ],
                } );
            }

            Guid userId = Guid.Parse( principal.FindFirstValue( ClaimTypes.NameIdentifier )! );

            // Студент всегда берётся из сессии — никаких id в параметрах.
            Guid? studentProfileId = await db.Set<StudentProfile>()
                .Where( p => p.UserId == userId )
                .Select( p => (Guid?)p.Id )
                .SingleOrDefaultAsync( cancellationToken );

            if ( studentProfileId is null )
            {
                return ProfileNotFound();
            }

            List<Assessment> assessments = await db.Set<Assessment>()
                .Where( a => a.StudentProfileId == studentProfileId && a.Semester == semester )
                .Include( a => a.Discipline )
                .OrderBy( a => a.Discipline.Name )
                .ThenBy( a => a.ControlForm )
                .AsNoTracking()
                .ToListAsync( cancellationToken );

            GradesSummaryDto summary = BuildSummary( assessments );

            AssessmentDto[] items =
            [
                .. assessments.Select( a => new AssessmentDto(
                    a.DisciplineId,
                    a.Discipline.Name,
                    a.ControlForm,
                    a.Grade,
                    a.IsPassed,
                    a.Points,
                    a.Date,
                    a.Status ) ),
            ];

            return TypedResults.Ok( new GradesResponse( semester.Value, summary, items ) );
        }

        // GET /api/session?semester=N
        private static async Task<IResult> GetSessionAsync(
            int? semester,
            ClaimsPrincipal principal,
            AppDbContext db,
            UniversityClock clock,
            CancellationToken cancellationToken )
        {
            if ( semester is null or < 1 or > 20 )
            {
                return TypedResults.ValidationProblem( new Dictionary<string, string[]>
                {
                    [ "semester" ] = [ "Укажите номер семестра (целое число от 1 до 20)" ],
                } );
            }

            Guid userId = Guid.Parse( principal.FindFirstValue( ClaimTypes.NameIdentifier )! );

            Guid? groupId = await db.Set<StudentProfile>()
                .Where( p => p.UserId == userId )
                .Select( p => (Guid?)p.GroupId )
                .SingleOrDefaultAsync( cancellationToken );

            if ( groupId is null )
            {
                return ProfileNotFound();
            }

            List<SessionEvent> events = await db.Set<SessionEvent>()
                .Where( e => e.GroupId == groupId && e.Semester == semester )
                .Include( e => e.Discipline )
                .Include( e => e.Teacher )
                .OrderBy( e => e.StartsAt )
                .AsNoTracking()
                .ToListAsync( cancellationToken );

            SessionEventDto[] items =
            [
                .. events.Select( e => new SessionEventDto(
                    e.Id,
                    e.Discipline.Name,
                    e.Type,
                    e.StartsAt,
                    e.EndsAt,
                    e.Room,
                    Schedule.ScheduleEndpoints.ShortName( e.Teacher.FullName ) ) ),
            ];

            return TypedResults.Ok( new SessionResponse( semester.Value, clock.ZoneId, items ) );
        }

        // ──────────────────────────── Helpers ─────────────────────────────

        /// <summary>
        /// Вычисляет сводку: средний балл только по числовым оценкам (Exam, GradedCredit),
        /// число долгов и число ещё не сданных дисциплин.
        /// </summary>
        internal static GradesSummaryDto BuildSummary( IReadOnlyList<Assessment> assessments )
        {
            List<int> numericGrades = assessments
                .Where( a => a.Status == AssessmentStatus.Passed
                             && a.Grade is not null
                             && a.Grade >= 3 )
                .Select( a => a.Grade!.Value )
                .ToList();

            double? average = numericGrades.Count > 0
                ? Math.Round( numericGrades.Average(), 2 )
                : null;

            int debts = assessments.Count( a => a.Status == AssessmentStatus.Debt );

            int remaining = assessments.Count( a =>
                a.Status is AssessmentStatus.Failed or AssessmentStatus.NotAdmitted
                || ( a.Date is null && a.Status != AssessmentStatus.Passed ) );

            return new GradesSummaryDto( average, debts, remaining );
        }

        private static IResult ProfileNotFound()
        {
            return TypedResults.Problem(
                title: "Профиль не найден",
                detail: "Для этой учётной записи профиль студента ещё не заведён. Обратитесь в деканат.",
                statusCode: StatusCodes.Status404NotFound );
        }
    }
}
