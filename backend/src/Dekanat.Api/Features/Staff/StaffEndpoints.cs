using System.Security.Claims;
using System.Text.Json;
using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Profile;
using Dekanat.Api.Features.Requests;
using Dekanat.Api.Features.Notifications;
using Dekanat.Api.Features.Schedule;
using Dekanat.Api.Infrastructure.Persistence;
using Dekanat.Api.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace Dekanat.Api.Features.Staff
{
    /// <param name="Id">Id профиля студента.</param>
    /// <param name="FullName">ФИО студента.</param>
    /// <param name="Group">Название группы.</param>
    public sealed record StaffRequestStudentDto( Guid Id, string FullName, string Group );

    /// <param name="Id">Id заявки, UUID.</param>
    /// <param name="Student">Кто подал заявку.</param>
    /// <param name="Type">Тип заявки.</param>
    /// <param name="Status">Статус сейчас.</param>
    /// <param name="CreatedAt">Когда подана, UTC.</param>
    /// <param name="EstimatedReadyDate">Примерная дата готовности по времени вуза.</param>
    /// <param name="Fields">Значения полей, как их заполнил студент.</param>
    public sealed record StaffRequestDto(
        Guid Id,
        StaffRequestStudentDto Student,
        RequestTypeRefDto Type,
        RequestStatus Status,
        DateTime CreatedAt,
        DateOnly? EstimatedReadyDate,
        JsonElement Fields );

    /// <param name="Items">Заявки факультета сотрудника, старые сверху.</param>
    public sealed record StaffRequestsResponse( IReadOnlyList<StaffRequestDto> Items );

    /// <param name="Id">Id заявки.</param>
    /// <param name="Student">Кто подал заявку.</param>
    /// <param name="Type">Тип заявки.</param>
    /// <param name="Status">Статус сейчас.</param>
    /// <param name="CreatedAt">Когда подана, UTC.</param>
    /// <param name="EstimatedReadyDate">Примерная дата готовности.</param>
    /// <param name="IssuePlace">Где забрать. Есть, когда заявка готова.</param>
    /// <param name="IssueDeadline">До какого времени забрать, UTC.</param>
    /// <param name="Fields">Значения полей.</param>
    /// <param name="History">История статусов от первого к последнему.</param>
    /// <param name="AvailableTransitions">Куда сотрудник может перевести заявку сейчас.</param>
    public sealed record StaffRequestDetailsDto(
        Guid Id,
        StaffRequestStudentDto Student,
        RequestTypeRefDto Type,
        RequestStatus Status,
        DateTime CreatedAt,
        DateOnly? EstimatedReadyDate,
        string? IssuePlace,
        DateTime? IssueDeadline,
        JsonElement Fields,
        IReadOnlyList<RequestHistoryEntryDto> History,
        IReadOnlyList<StatusTransitionDto> AvailableTransitions );

    /// <param name="Status">Статус, в который можно перейти.</param>
    /// <param name="Title">Название кнопки для интерфейса.</param>
    public sealed record StatusTransitionDto( RequestStatus Status, string Title );

    /// <param name="Status">Новый статус.</param>
    /// <param name="Comment">Комментарий. Обязателен при отказе (причина отказа).</param>
    /// <param name="IssuePlace">Где забрать. Обязателен при переходе в «Готова».</param>
    /// <param name="IssueDeadline">До какого времени забрать, UTC. Обязателен при переходе в «Готова».</param>
    public sealed record ChangeStatusBody(
        RequestStatus Status,
        string? Comment,
        string? IssuePlace,
        DateTime? IssueDeadline );

    /// <summary>Ответ после смены статуса: только данные самой заявки без полного объекта студента.</summary>
    public sealed record StaffStatusChangedDto(
        Guid Id,
        RequestStatus Status,
        DateTime CreatedAt,
        DateOnly? EstimatedReadyDate,
        string? IssuePlace,
        DateTime? IssueDeadline,
        JsonElement Fields,
        IReadOnlyList<RequestHistoryEntryDto> History );

    /// <param name="Title">Заголовок объявления.</param>
    /// <param name="Content">Текст объявления.</param>
    /// <param name="IsImportant">Важное объявление попадает на главный экран.</param>
    /// <param name="ExpiresAt">До какого момента актуально, UTC.</param>
    /// <param name="Audience">Кому показывать.</param>
    public sealed record CreateAnnouncementBody(
        string Title,
        string Content,
        bool IsImportant,
        DateTime ExpiresAt,
        AnnouncementAudienceBody Audience );

    /// <param name="Type">All, Faculty, Group.</param>
    /// <param name="GroupId">Id группы. Нужен только при Type=Group.</param>
    public sealed record AnnouncementAudienceBody( AnnouncementAudienceType Type, Guid? GroupId );

    /// <param name="Id">Id объявления.</param>
    /// <param name="Title">Заголовок.</param>
    /// <param name="Content">Текст.</param>
    /// <param name="PublishedAt">Когда создано, UTC.</param>
    /// <param name="ExpiresAt">До какого момента актуально, UTC.</param>
    /// <param name="IsImportant">Важное объявление.</param>
    /// <param name="Audience">Аудитория.</param>
    public sealed record AnnouncementDto(
        Guid Id,
        string Title,
        string Content,
        DateTime PublishedAt,
        DateTime ExpiresAt,
        bool IsImportant,
        AnnouncementAudienceDto Audience );

    /// <param name="Type">All, Faculty, Group.</param>
    /// <param name="FacultyId">Id факультета. Есть при Type=Faculty и Type=Group.</param>
    /// <param name="FacultyName">Название факультета.</param>
    /// <param name="GroupId">Id группы. Есть при Type=Group.</param>
    /// <param name="GroupName">Название группы.</param>
    public sealed record AnnouncementAudienceDto(
        AnnouncementAudienceType Type,
        Guid? FacultyId,
        string? FacultyName,
        Guid? GroupId,
        string? GroupName );

    /// <param name="StartsAt">Новое начало пары, UTC.</param>
    /// <param name="EndsAt">Новый конец пары, UTC.</param>
    /// <param name="Room">Новая аудитория.</param>
    /// <param name="Reason">Причина переноса.</param>
    public sealed record ChangeLessonBody(
        DateTime StartsAt,
        DateTime EndsAt,
        string Room,
        string? Reason );

    /// <summary>Ответ после переноса/отмены пары.</summary>
    public sealed record LessonChangedDto(
        Guid Id,
        DateTime StartsAt,
        DateTime EndsAt,
        string Room,
        LessonType Type,
        bool IsCancelled );

    public static class StaffEndpoints
    {
        /// <summary>
        /// Всё под /api/staff доступно только роли Staff: политика стоит на группе, поэтому новый эндпоинт
        /// сотрудника не получится выпустить без неё. Студент получает 403, без входа 401.
        /// </summary>
        public static RouteGroupBuilder MapStaff( this RouteGroupBuilder api )
        {
            RouteGroupBuilder staff = api.MapGroup( "/staff" )
                .WithTags( "Staff" )
                .RequireAuthorization( AuthorizationPolicies.Staff )
                .ProducesProblem( StatusCodes.Status401Unauthorized )
                .ProducesProblem( StatusCodes.Status403Forbidden );

            staff.MapGet( "/requests", GetRequestsAsync )
                .WithName( "GetStaffRequests" )
                .WithSummary( "Очередь заявок факультета того, кто вошёл. Факультет сервер берёт только из сессии" )
                .Produces<StaffRequestsResponse>()
                .ProducesProblem( StatusCodes.Status404NotFound );

            staff.MapGet( "/requests/{id:guid}", GetRequestAsync )
                .WithName( "GetStaffRequest" )
                .WithSummary( "Карточка заявки для сотрудника. Не своего факультета и несуществующая одинаково дают 404" )
                .Produces<StaffRequestDetailsDto>()
                .ProducesProblem( StatusCodes.Status404NotFound );

            staff.MapPost( "/requests/{id:guid}/status", ChangeStatusAsync )
                .WithName( "ChangeRequestStatus" )
                .WithSummary( "Меняет статус заявки. При переводе в Ready обязательны issuePlace и issueDeadline" )
                .Produces<StaffStatusChangedDto>()
                .ProducesValidationProblem()
                .ProducesProblem( StatusCodes.Status404NotFound )
                .ProducesProblem( StatusCodes.Status409Conflict );

            staff.MapPost( "/announcements", CreateAnnouncementAsync )
                .WithName( "CreateAnnouncement" )
                .WithSummary( "Создаёт объявление для выбранной аудитории: все, факультет или группа" )
                .Produces<AnnouncementDto>( StatusCodes.Status201Created )
                .ProducesValidationProblem()
                .ProducesProblem( StatusCodes.Status404NotFound );

            staff.MapPost( "/lessons/{id:guid}/change", ChangeLessonAsync )
                .WithName( "ChangeLesson" )
                .WithSummary( "Переносит пару. Если новая аудитория занята в выбранное время, возвращает 409" )
                .Produces<LessonChangedDto>()
                .ProducesValidationProblem()
                .ProducesProblem( StatusCodes.Status404NotFound )
                .ProducesProblem( StatusCodes.Status409Conflict );

            return api;
        }

        // ── GET /api/staff/requests ───────────────────────────────────────────

        /// <param name="status">Фильтр по статусу. Без параметра — все статусы.</param>
        /// <param name="type">Фильтр по типу заявки (Id типа).</param>
        /// <param name="group">Фильтр по названию группы.</param>
        /// <param name="search">Поиск по ФИО студента (contains, без учёта регистра).</param>
        private static async Task<IResult> GetRequestsAsync(
            RequestStatus? status,
            Guid? type,
            string? group,
            string? search,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken cancellationToken )
        {
            Guid? facultyId = await StaffScope.FacultyIdAsync( principal, db, cancellationToken );
            if ( facultyId is null )
            {
                return StaffScope.ProfileNotFound();
            }

            // Факультет фиксирован из сессии: заявки других факультетов в выборку не попадают.
            IQueryable<Request> query = db.Set<Request>()
                .AsNoTracking()
                .InFaculty( facultyId.Value );

            if ( status is not null )
            {
                RequestStatus wanted = status.Value;
                query = query.Where( r => r.CurrentStatus == wanted );
            }

            if ( type is not null )
            {
                Guid wantedType = type.Value;
                query = query.Where( r => r.RequestTypeId == wantedType );
            }

            if ( !string.IsNullOrWhiteSpace( group ) )
            {
                string wantedGroup = group.Trim();
                query = query.Where( r => r.StudentProfile.Group.Name == wantedGroup );
            }

            if ( !string.IsNullOrWhiteSpace( search ) )
            {
                string term = search.Trim().ToLower();
                query = query.Where( r => r.StudentProfile.FullName.ToLower().Contains( term ) );
            }

            var rows = await query
                .OrderBy( r => r.CreatedAt )
                .ThenBy( r => r.Id )
                .Select( r => new
                {
                    r.Id,
                    StudentId = r.StudentProfileId,
                    r.StudentProfile.FullName,
                    Group = r.StudentProfile.Group.Name,
                    r.RequestTypeId,
                    TypeName = r.Type.Name,
                    r.CurrentStatus,
                    r.CreatedAt,
                    r.EstimatedReadyDate,
                    r.Fields,
                } )
                .ToListAsync( cancellationToken );

            return TypedResults.Ok( new StaffRequestsResponse(
                [ .. rows.Select( row => new StaffRequestDto(
                    row.Id,
                    new StaffRequestStudentDto( row.StudentId, row.FullName, row.Group ),
                    new RequestTypeRefDto( row.RequestTypeId, row.TypeName ),
                    row.CurrentStatus,
                    row.CreatedAt,
                    row.EstimatedReadyDate,
                    RequestEndpoints.ParseJson( row.Fields ) ) ) ] ) );
        }

        // ── GET /api/staff/requests/{id} ──────────────────────────────────────

        private static async Task<IResult> GetRequestAsync(
            Guid id,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken cancellationToken )
        {
            Guid? facultyId = await StaffScope.FacultyIdAsync( principal, db, cancellationToken );

            // Не своего факультета и несуществующая — одинаково 404.
            Request? request = facultyId is null
                ? null
                : await db.Set<Request>()
                    .InFaculty( facultyId.Value )
                    .Include( r => r.Type )
                    .Include( r => r.History )
                    .Include( r => r.StudentProfile ).ThenInclude( sp => sp.Group )
                    .AsNoTracking()
                    .SingleOrDefaultAsync( r => r.Id == id, cancellationToken );

            if ( request is null )
            {
                return RequestNotFound();
            }

            return TypedResults.Ok( ToDetails( request ) );
        }

        // ── POST /api/staff/requests/{id}/status ──────────────────────────────

        private static async Task<IResult> ChangeStatusAsync(
            Guid id,
            ChangeStatusBody body,
            ClaimsPrincipal principal,
            AppDbContext db,
            UniversityClock clock,
            CancellationToken cancellationToken )
        {
            Guid? facultyId = await StaffScope.FacultyIdAsync( principal, db, cancellationToken );

            Request? request = facultyId is null
                ? null
                : await db.Set<Request>()
                    .InFaculty( facultyId.Value )
                    .Include( r => r.History )
                    .Include( r => r.StudentProfile )
                    .Include( r => r.Type )
                    .SingleOrDefaultAsync( r => r.Id == id, cancellationToken );

            if ( request is null )
            {
                return RequestNotFound();
            }

            Guid staffUserId = UserId( principal );
            TransitionResult result = RequestWorkflow.Apply(
                request,
                new StatusChange(
                    body.Status,
                    RequestActor.Staff,
                    staffUserId,
                    body.Comment,
                    body.IssuePlace,
                    body.IssueDeadline ),
                clock.UtcNow );

            if ( result.ToProblem() is { } problem )
            {
                return problem;
            }

            // Уведомление создаётся в той же транзакции, что и смена статуса.
            Notification statusNotification = NotificationFactory.RequestStatusChanged(
                request.StudentProfile.UserId,
                request.Id,
                body.Status,
                request.Type.Name,
                body.Comment,
                clock.UtcNow );
            db.Add( statusNotification );

            try
            {
                await db.SaveChangesAsync( cancellationToken );
            }
            catch ( DbUpdateConcurrencyException )
            {
                return TypedResults.Problem(
                    title: "Статус заявки сейчас нельзя изменить",
                    detail: "Заявка только что изменилась. Обновите страницу и проверьте её статус.",
                    statusCode: StatusCodes.Status409Conflict );
            }

            var history = request.History
                .OrderBy( e => e.ChangedAt )
                .Select( e => new RequestHistoryEntryDto( e.Status, e.Comment, e.ChangedAt ) )
                .ToList();

            return TypedResults.Ok( new StaffStatusChangedDto(
                request.Id,
                request.CurrentStatus,
                request.CreatedAt,
                request.EstimatedReadyDate,
                request.IssuePlace,
                request.IssueDeadline,
                RequestEndpoints.ParseJson( request.Fields ),
                history ) );
        }

        // ── POST /api/staff/announcements ─────────────────────────────────────

        private static async Task<IResult> CreateAnnouncementAsync(
            CreateAnnouncementBody body,
            ClaimsPrincipal principal,
            AppDbContext db,
            UniversityClock clock,
            CancellationToken cancellationToken )
        {
            Guid? facultyId = await StaffScope.FacultyIdAsync( principal, db, cancellationToken );
            if ( facultyId is null )
            {
                return StaffScope.ProfileNotFound();
            }

            // Валидация аудитории
            Dictionary<string, string[]> errors = [];

            if ( string.IsNullOrWhiteSpace( body.Title ) )
            {
                errors[ "title" ] = [ "Укажите заголовок объявления" ];
            }
            else if ( body.Title.Length > 200 )
            {
                errors[ "title" ] = [ "Заголовок не длиннее 200 символов" ];
            }

            if ( string.IsNullOrWhiteSpace( body.Content ) )
            {
                errors[ "content" ] = [ "Укажите текст объявления" ];
            }
            else if ( body.Content.Length > 5000 )
            {
                errors[ "content" ] = [ "Текст не длиннее 5000 символов" ];
            }

            if ( body.ExpiresAt <= clock.UtcNow )
            {
                errors[ "expiresAt" ] = [ "Срок актуальности должен быть в будущем" ];
            }

            Guid? groupId = null;
            Guid announcementFacultyId = facultyId.Value;
            string? groupName = null;
            string? facultyName = null;

            if ( body.Audience.Type == AnnouncementAudienceType.Group )
            {
                if ( body.Audience.GroupId is null )
                {
                    errors[ "audience.groupId" ] = [ "Укажите группу" ];
                }
                else
                {
                    var grp = await db.Set<StudyGroup>()
                        .AsNoTracking()
                        .Where( g => g.Id == body.Audience.GroupId.Value && g.Program.FacultyId == facultyId.Value )
                        .Select( g => new { g.Id, g.Name, FacultyId = g.Program.FacultyId, FacultyName = g.Program.Faculty.Name } )
                        .SingleOrDefaultAsync( cancellationToken );

                    if ( grp is null )
                    {
                        errors[ "audience.groupId" ] = [ "Группа не найдена или не относится к вашему факультету" ];
                    }
                    else
                    {
                        groupId = grp.Id;
                        groupName = grp.Name;
                        announcementFacultyId = grp.FacultyId;
                        facultyName = grp.FacultyName;
                    }
                }
            }
            else if ( body.Audience.Type == AnnouncementAudienceType.Faculty )
            {
                facultyName = await db.Set<Faculty>()
                    .AsNoTracking()
                    .Where( f => f.Id == facultyId.Value )
                    .Select( f => f.Name )
                    .SingleOrDefaultAsync( cancellationToken );
            }

            if ( errors.Count > 0 )
            {
                return TypedResults.ValidationProblem( errors );
            }

            Announcement announcement = new()
            {
                Id = Guid.NewGuid(),
                Title = body.Title.Trim(),
                Content = body.Content.Trim(),
                IsImportant = body.IsImportant,
                PublishedAt = clock.UtcNow,
                ExpiresAt = body.ExpiresAt,
                AuthorStaffProfileId = await db.Set<StaffProfile>()
                    .Where( p => p.UserId == UserId( principal ) )
                    .Select( p => ( Guid? )p.Id )
                    .SingleOrDefaultAsync( cancellationToken ),
                AudienceType = body.Audience.Type,
                FacultyId = body.Audience.Type != AnnouncementAudienceType.All ? announcementFacultyId : null,
                GroupId = groupId,
            };

            db.Add( announcement );
            await db.SaveChangesAsync( cancellationToken );

            AnnouncementAudienceDto audience = new(
                body.Audience.Type,
                body.Audience.Type != AnnouncementAudienceType.All ? announcementFacultyId : null,
                facultyName,
                groupId,
                groupName );

            AnnouncementDto dto = new(
                announcement.Id,
                announcement.Title,
                announcement.Content,
                announcement.PublishedAt,
                announcement.ExpiresAt,
                announcement.IsImportant,
                audience );

            return TypedResults.Created( $"/api/staff/announcements/{announcement.Id}", dto );
        }

        // ── POST /api/staff/lessons/{id}/change ───────────────────────────────

        private static async Task<IResult> ChangeLessonAsync(
            Guid id,
            ChangeLessonBody body,
            ClaimsPrincipal principal,
            AppDbContext db,
            UniversityClock clock,
            CancellationToken cancellationToken )
        {
            Guid? facultyId = await StaffScope.FacultyIdAsync( principal, db, cancellationToken );
            if ( facultyId is null )
            {
                return StaffScope.ProfileNotFound();
            }

            // Валидация тела
            Dictionary<string, string[]> errors = [];

            if ( body.EndsAt <= body.StartsAt )
            {
                errors[ "endsAt" ] = [ "Конец пары должен быть позже начала" ];
            }

            if ( string.IsNullOrWhiteSpace( body.Room ) )
            {
                errors[ "room" ] = [ "Укажите аудиторию" ];
            }
            else if ( body.Room.Length > 32 )
            {
                errors[ "room" ] = [ "Аудитория не длиннее 32 символов" ];
            }

            if ( body.Reason is { Length: > 300 } )
            {
                errors[ "reason" ] = [ "Причина не длиннее 300 символов" ];
            }

            if ( errors.Count > 0 )
            {
                return TypedResults.ValidationProblem( errors );
            }

            // Пара должна принадлежать факультету сотрудника (через группу → программу → факультет)
            Lesson? lesson = await db.Set<Lesson>()
                .Include( l => l.Changes )
                .Include( l => l.Discipline )
                .SingleOrDefaultAsync(
                    l => l.Id == id && l.Group.Program.FacultyId == facultyId.Value,
                    cancellationToken );

            if ( lesson is null )
            {
                return TypedResults.Problem(
                    title: "Пара не найдена",
                    detail: "Пара не найдена или не относится к вашему факультету.",
                    statusCode: StatusCodes.Status404NotFound );
            }

            // Проверяем занятость аудитории (кроме самой этой пары)
            string room = body.Room.Trim();
            bool roomBusy = await db.Set<Lesson>()
                .AnyAsync(
                    l => l.Id != id
                        && l.Room == room
                        && !l.IsCancelled
                        && l.StartsAt < body.EndsAt
                        && l.EndsAt > body.StartsAt,
                    cancellationToken );

            if ( roomBusy )
            {
                return TypedResults.Problem(
                    title: "Аудитория занята",
                    detail: $"Аудитория {room} занята в выбранное время.",
                    statusCode: StatusCodes.Status409Conflict );
            }

            // Запоминаем оригинальные значения для записи изменения
            DateTime? oldStartsAt = lesson.Changes.Count == 0 ? lesson.StartsAt : null;
            string? oldRoom = lesson.Changes.All( c => c.OldRoom is null ) ? lesson.Room : null;

            LessonChangeType changeType = body.StartsAt != lesson.StartsAt
                ? LessonChangeType.Reschedule
                : LessonChangeType.RoomChange;

            lesson.StartsAt = body.StartsAt;
            lesson.EndsAt = body.EndsAt;
            lesson.Room = room;

            LessonChange change = new()
            {
                LessonId = lesson.Id,
                Type = changeType,
                OldStartsAt = changeType == LessonChangeType.Reschedule ? oldStartsAt : null,
                NewStartsAt = changeType == LessonChangeType.Reschedule ? body.StartsAt : null,
                OldRoom = room != ( oldRoom ?? lesson.Room ) ? oldRoom : null,
                NewRoom = room != ( oldRoom ?? lesson.Room ) ? room : null,
                Reason = string.IsNullOrWhiteSpace( body.Reason ) ? null : body.Reason.Trim(),
                ChangedAt = clock.UtcNow,
                ChangedByUserId = UserId( principal ),
            };
            lesson.Changes.Add( change );

            // Уведомляем всех студентов группы. Запрос в той же транзакции, что и изменение пары.
            List<Guid> groupStudentUserIds = await db.Set<StudentProfile>()
                .AsNoTracking()
                .Where( sp => sp.GroupId == lesson.GroupId )
                .Select( sp => sp.UserId )
                .ToListAsync( cancellationToken );

            IReadOnlyList<Notification> lessonNotifications = NotificationFactory.LessonChanged(
                groupStudentUserIds,
                lesson.Id,
                lesson.Discipline.Name,
                changeType,
                changeType == LessonChangeType.Reschedule ? ( DateTime? )body.StartsAt : null,
                room,
                body.Reason,
                clock.UtcNow );
            db.AddRange( lessonNotifications );

            await db.SaveChangesAsync( cancellationToken );

            return TypedResults.Ok( new LessonChangedDto(
                lesson.Id,
                lesson.StartsAt,
                lesson.EndsAt,
                lesson.Room,
                lesson.Type,
                lesson.IsCancelled ) );
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Guid UserId( ClaimsPrincipal principal )
        {
            return Guid.Parse( principal.FindFirstValue( ClaimTypes.NameIdentifier )! );
        }

        private static StaffRequestDetailsDto ToDetails( Request request )
        {
            IReadOnlyList<StatusTransitionDto> transitions =
            [
                .. RequestWorkflow.NextStatuses( request.CurrentStatus, RequestActor.Staff )
                    .Select( s => new StatusTransitionDto( s, RequestWorkflow.Title( s ) ) ),
            ];

            return new StaffRequestDetailsDto(
                request.Id,
                new StaffRequestStudentDto(
                    request.StudentProfileId,
                    request.StudentProfile.FullName,
                    request.StudentProfile.Group.Name ),
                new RequestTypeRefDto( request.RequestTypeId, request.Type.Name ),
                request.CurrentStatus,
                request.CreatedAt,
                request.EstimatedReadyDate,
                request.IssuePlace,
                request.IssueDeadline,
                RequestEndpoints.ParseJson( request.Fields ),
                [ .. request.History.OrderBy( e => e.ChangedAt ).Select( e => new RequestHistoryEntryDto( e.Status, e.Comment, e.ChangedAt ) ) ],
                transitions );
        }

        private static IResult RequestNotFound()
        {
            return TypedResults.Problem(
                title: "Заявка не найдена",
                detail: "Заявка не найдена или недоступна.",
                statusCode: StatusCodes.Status404NotFound );
        }
    }
}
