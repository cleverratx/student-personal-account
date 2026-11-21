using System.Security.Claims;
using System.Text.Json;
using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Profile;
using Dekanat.Api.Features.Requests;
using Dekanat.Api.Features.Schedule;
using Dekanat.Api.Infrastructure.Persistence;
using Dekanat.Api.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace Dekanat.Api.Features.Staff
{
    // ── Заявки ─────────────────────────────────────────────────────────────────

    /// <param name="Id">Id профиля студента.</param>
    /// <param name="FullName">ФИО студента.</param>
    /// <param name="Group">Название группы.</param>
    public sealed record StaffRequestStudentDto( Guid Id, string FullName, string Group );

    /// <param name="Id">Id заявки.</param>
    /// <param name="Student">Кто подал заявку.</param>
    /// <param name="Type">Тип заявки.</param>
    /// <param name="Status">Текущий статус.</param>
    /// <param name="CreatedAt">Когда подана, UTC.</param>
    /// <param name="EstimatedReadyDate">Примерная дата готовности.</param>
    /// <param name="Fields">Значения полей студента.</param>
    public sealed record StaffRequestDto(
        Guid Id,
        StaffRequestStudentDto Student,
        RequestTypeRefDto Type,
        RequestStatus Status,
        DateTime CreatedAt,
        DateOnly? EstimatedReadyDate,
        JsonElement Fields );

    /// <param name="Items">Заявки факультета, от старых к новым (очередь).</param>
    public sealed record StaffRequestsResponse( IReadOnlyList<StaffRequestDto> Items );

    /// <param name="Id">Id заявки.</param>
    /// <param name="Student">Кто подал.</param>
    /// <param name="Type">Тип заявки.</param>
    /// <param name="Status">Текущий статус.</param>
    /// <param name="CreatedAt">Когда подана, UTC.</param>
    /// <param name="EstimatedReadyDate">Примерная дата готовности.</param>
    /// <param name="IssuePlace">Где забрать. Есть, когда заявка готова.</param>
    /// <param name="IssueDeadline">До какого времени забрать, UTC.</param>
    /// <param name="Fields">Значения полей студента.</param>
    /// <param name="History">История статусов.</param>
    /// <param name="AvailableTransitions">Куда сотрудник может перевести заявку сейчас. Пустой список — статус финальный.</param>
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
        IReadOnlyList<RequestStatus> AvailableTransitions );

    /// <param name="Status">Новый статус заявки.</param>
    /// <param name="Comment">Комментарий. Обязателен при статусе Rejected — это причина отказа.</param>
    /// <param name="IssuePlace">Где забрать. Обязателен при статусе Ready.</param>
    /// <param name="IssueDeadline">До какого времени забрать, UTC. Обязателен при статусе Ready.</param>
    public sealed record ChangeStatusBody(
        RequestStatus Status,
        string? Comment = null,
        string? IssuePlace = null,
        DateTime? IssueDeadline = null );

    // ── Объявления ─────────────────────────────────────────────────────────────

    /// <param name="Type">All, Faculty или Group.</param>
    /// <param name="GroupId">Id группы. Только для type=Group.</param>
    public sealed record AudienceBody( AudienceType Type, Guid? GroupId = null );

    /// <param name="Title">Заголовок до 200 символов.</param>
    /// <param name="Content">Текст до 2000 символов.</param>
    /// <param name="IsImportant">Важное объявление.</param>
    /// <param name="ExpiresAt">До какого момента актуально, UTC. Null — бессрочно.</param>
    /// <param name="Audience">Кому: все, факультет или конкретная группа.</param>
    public sealed record CreateAnnouncementBody(
        string Title,
        string Content,
        bool IsImportant,
        DateTime? ExpiresAt,
        AudienceBody Audience );

    /// <param name="Id">Id объявления.</param>
    /// <param name="Title">Заголовок.</param>
    /// <param name="Content">Текст.</param>
    /// <param name="IsImportant">Важное.</param>
    /// <param name="PublishedAt">Когда опубликовано, UTC.</param>
    /// <param name="ExpiresAt">До какого момента актуально, UTC.</param>
    /// <param name="AudienceType">All, Faculty или Group.</param>
    /// <param name="FacultyId">Id факультета. Не null, когда AudienceType=Faculty.</param>
    /// <param name="GroupId">Id группы. Не null, когда AudienceType=Group.</param>
    /// <param name="GroupName">Название группы. Не null, когда AudienceType=Group.</param>
    public sealed record AnnouncementDto(
        Guid Id,
        string Title,
        string Content,
        bool IsImportant,
        DateTime PublishedAt,
        DateTime? ExpiresAt,
        AudienceType AudienceType,
        Guid? FacultyId,
        Guid? GroupId,
        string? GroupName );

    // ── Расписание ─────────────────────────────────────────────────────────────

    /// <param name="StartsAt">Новое начало пары, UTC. Null — время остаётся прежним.</param>
    /// <param name="EndsAt">Новый конец пары, UTC. Null — время остаётся прежним.</param>
    /// <param name="Room">Новая аудитория. Null — аудитория остаётся прежней.</param>
    /// <param name="Reason">Причина изменения.</param>
    public sealed record ChangeLessonBody(
        DateTime? StartsAt,
        DateTime? EndsAt,
        string? Room,
        string? Reason );

    /// <param name="Id">Id пары.</param>
    /// <param name="StartsAt">Начало, UTC.</param>
    /// <param name="EndsAt">Конец, UTC.</param>
    /// <param name="Room">Аудитория.</param>
    /// <param name="Type">Тип пары.</param>
    /// <param name="IsCancelled">Пара отменена.</param>
    public sealed record ChangedLessonDto(
        Guid Id,
        DateTime StartsAt,
        DateTime EndsAt,
        string Room,
        LessonType Type,
        bool IsCancelled );

    // ── Маршруты ───────────────────────────────────────────────────────────────

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
                .WithSummary( "Карточка заявки с историей и доступными переходами. Чужой факультет и несуществующая заявка дают 404" )
                .Produces<StaffRequestDetailsDto>()
                .ProducesProblem( StatusCodes.Status404NotFound );

            staff.MapPost( "/requests/{id:guid}/status", ChangeStatusAsync )
                .WithName( "ChangeRequestStatus" )
                .WithSummary( "Меняет статус заявки. При переводе в Ready обязательны issuePlace и issueDeadline" )
                .Produces<StaffRequestDetailsDto>()
                .ProducesValidationProblem()
                .ProducesProblem( StatusCodes.Status404NotFound )
                .ProducesProblem( StatusCodes.Status409Conflict );

            staff.MapPost( "/announcements", CreateAnnouncementAsync )
                .WithName( "CreateAnnouncement" )
                .WithSummary( "Создаёт объявление. Группа должна принадлежать факультету сотрудника" )
                .Produces<AnnouncementDto>( StatusCodes.Status201Created )
                .ProducesValidationProblem()
                .ProducesProblem( StatusCodes.Status404NotFound );

            staff.MapPost( "/lessons/{id:guid}/change", ChangeLessonAsync )
                .WithName( "ChangeLesson" )
                .WithSummary( "Переносит пару или меняет аудиторию. 409 если аудитория занята в выбранное время" )
                .Produces<ChangedLessonDto>()
                .ProducesValidationProblem()
                .ProducesProblem( StatusCodes.Status404NotFound )
                .ProducesProblem( StatusCodes.Status409Conflict );

            return api;
        }

        // ── Обработчики ────────────────────────────────────────────────────────

        /// <param name="status">Фильтр по статусу.</param>
        /// <param name="type">Фильтр по id типа заявки.</param>
        /// <param name="group">Фильтр по названию группы (точное совпадение, регистр не важен).</param>
        /// <param name="search">Поиск по ФИО студента (подстрока, регистр не важен).</param>
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

            // Фильтр по факультету в самом запросе: заявки других факультетов не попадают.
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
                string wantedGroup = group.ToLowerInvariant();
                query = query.Where( r => r.StudentProfile.Group.Name.ToLower() == wantedGroup );
            }

            if ( !string.IsNullOrWhiteSpace( search ) )
            {
                string term = search.ToLowerInvariant();
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

            return TypedResults.Ok( new StaffRequestsResponse( [ .. rows.Select( row => new StaffRequestDto(
                row.Id,
                new StaffRequestStudentDto( row.StudentId, row.FullName, row.Group ),
                new RequestTypeRefDto( row.RequestTypeId, row.TypeName ),
                row.CurrentStatus,
                row.CreatedAt,
                row.EstimatedReadyDate,
                RequestEndpoints.ParseJson( row.Fields ) ) ) ] ) );
        }

        private static async Task<IResult> GetRequestAsync(
            Guid id,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken cancellationToken )
        {
            Guid? facultyId = await StaffScope.FacultyIdAsync( principal, db, cancellationToken );
            if ( facultyId is null )
            {
                return StaffScope.ProfileNotFound();
            }

            var row = await db.Set<Request>()
                .AsNoTracking()
                .InFaculty( facultyId.Value )
                .Include( r => r.History )
                .Include( r => r.Type )
                .Where( r => r.Id == id )
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
                    r.IssuePlace,
                    r.IssueDeadline,
                    r.Fields,
                    History = r.History.OrderBy( h => h.ChangedAt ).ToList(),
                } )
                .SingleOrDefaultAsync( cancellationToken );

            if ( row is null )
            {
                return RequestNotFound();
            }

            IReadOnlyList<RequestStatus> transitions = RequestWorkflow.NextStatuses( row.CurrentStatus, RequestActor.Staff );
            return TypedResults.Ok( new StaffRequestDetailsDto(
                row.Id,
                new StaffRequestStudentDto( row.StudentId, row.FullName, row.Group ),
                new RequestTypeRefDto( row.RequestTypeId, row.TypeName ),
                row.CurrentStatus,
                row.CreatedAt,
                row.EstimatedReadyDate,
                row.IssuePlace,
                row.IssueDeadline,
                RequestEndpoints.ParseJson( row.Fields ),
                [ .. row.History.Select( h => new RequestHistoryEntryDto( h.Status, h.Comment, h.ChangedAt ) ) ],
                transitions ) );
        }

        private static async Task<IResult> ChangeStatusAsync(
            Guid id,
            ChangeStatusBody body,
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

            Guid userId = Guid.Parse( principal.FindFirstValue( ClaimTypes.NameIdentifier )! );

            Request? request = await db.Set<Request>()
                .InFaculty( facultyId.Value )
                .Include( r => r.Type )
                .Include( r => r.History )
                .SingleOrDefaultAsync( r => r.Id == id, cancellationToken );
            if ( request is null )
            {
                return RequestNotFound();
            }

            TransitionResult result = RequestWorkflow.Apply(
                request,
                new StatusChange( body.Status, RequestActor.Staff, userId, body.Comment, body.IssuePlace, body.IssueDeadline ),
                clock.UtcNow );
            if ( result.ToProblem() is { } problem )
            {
                return problem;
            }

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

            IReadOnlyList<RequestStatus> transitions = RequestWorkflow.NextStatuses( request.CurrentStatus, RequestActor.Staff );
            return TypedResults.Ok( new StaffRequestDetailsDto(
                request.Id,
                new StaffRequestStudentDto( request.StudentProfileId, request.StudentProfile?.FullName ?? string.Empty, request.StudentProfile?.Group?.Name ?? string.Empty ),
                new RequestTypeRefDto( request.RequestTypeId, request.Type.Name ),
                request.CurrentStatus,
                request.CreatedAt,
                request.EstimatedReadyDate,
                request.IssuePlace,
                request.IssueDeadline,
                RequestEndpoints.ParseJson( request.Fields ),
                [ .. request.History.OrderBy( h => h.ChangedAt ).Select( h => new RequestHistoryEntryDto( h.Status, h.Comment, h.ChangedAt ) ) ],
                transitions ) );
        }

        private static async Task<IResult> CreateAnnouncementAsync(
            CreateAnnouncementBody body,
            ClaimsPrincipal principal,
            AppDbContext db,
            UniversityClock clock,
            CancellationToken cancellationToken )
        {
            Guid userId = Guid.Parse( principal.FindFirstValue( ClaimTypes.NameIdentifier )! );
            var staff = await db.Set<StaffProfile>()
                .AsNoTracking()
                .Where( s => s.UserId == userId )
                .Select( s => new { s.Id, s.FacultyId } )
                .SingleOrDefaultAsync( cancellationToken );
            if ( staff is null )
            {
                return StaffScope.ProfileNotFound();
            }

            // Валидация аудитории
            Dictionary<string, string[]> errors = [];
            Guid? facultyId = null;
            Guid? groupId = null;
            string? groupName = null;

            if ( body.Audience.Type == AudienceType.Faculty )
            {
                facultyId = staff.FacultyId;
            }
            else if ( body.Audience.Type == AudienceType.Group )
            {
                if ( body.Audience.GroupId is null )
                {
                    errors[ "audience.groupId" ] = [ "Укажите группу" ];
                }
                else
                {
                    var grp = await db.Set<StudyGroup>()
                        .AsNoTracking()
                        .Where( g => g.Id == body.Audience.GroupId )
                        .Select( g => new { g.Id, g.Name, g.Program.FacultyId } )
                        .SingleOrDefaultAsync( cancellationToken );

                    if ( grp is null || grp.FacultyId != staff.FacultyId )
                    {
                        errors[ "audience.groupId" ] = [ "Группа не найдена или не принадлежит вашему факультету" ];
                    }
                    else
                    {
                        groupId = grp.Id;
                        groupName = grp.Name;
                    }
                }
            }

            if ( string.IsNullOrWhiteSpace( body.Title ) )
            {
                errors[ "title" ] = [ "Укажите заголовок" ];
            }
            else if ( body.Title.Length > 200 )
            {
                errors[ "title" ] = [ "Заголовок не длиннее 200 символов" ];
            }

            if ( string.IsNullOrWhiteSpace( body.Content ) )
            {
                errors[ "content" ] = [ "Укажите текст объявления" ];
            }
            else if ( body.Content.Length > 2000 )
            {
                errors[ "content" ] = [ "Текст не длиннее 2000 символов" ];
            }

            if ( body.ExpiresAt is not null && body.ExpiresAt <= clock.UtcNow )
            {
                errors[ "expiresAt" ] = [ "Срок актуальности должен быть в будущем" ];
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
                AudienceType = body.Audience.Type,
                FacultyId = facultyId,
                GroupId = groupId,
                AuthorStaffProfileId = staff.Id,
            };
            db.Add( announcement );
            await db.SaveChangesAsync( cancellationToken );

            return TypedResults.Created(
                $"/api/staff/announcements/{announcement.Id}",
                new AnnouncementDto(
                    announcement.Id,
                    announcement.Title,
                    announcement.Content,
                    announcement.IsImportant,
                    announcement.PublishedAt,
                    announcement.ExpiresAt,
                    announcement.AudienceType,
                    facultyId,
                    groupId,
                    groupName ) );
        }

        private static async Task<IResult> ChangeLessonAsync(
            Guid id,
            ChangeLessonBody body,
            ClaimsPrincipal principal,
            AppDbContext db,
            UniversityClock clock,
            CancellationToken cancellationToken )
        {
            Guid userId = Guid.Parse( principal.FindFirstValue( ClaimTypes.NameIdentifier )! );

            Lesson? lesson = await db.Set<Lesson>()
                .Include( l => l.Changes )
                .SingleOrDefaultAsync( l => l.Id == id, cancellationToken );
            if ( lesson is null )
            {
                return TypedResults.Problem(
                    title: "Пара не найдена",
                    detail: "Пара не найдена.",
                    statusCode: StatusCodes.Status404NotFound );
            }

            // Валидация
            Dictionary<string, string[]> errors = [];
            DateTime newStartsAt = body.StartsAt ?? lesson.StartsAt;
            DateTime newEndsAt = body.EndsAt ?? lesson.EndsAt;
            string newRoom = body.Room?.Trim() ?? lesson.Room;

            if ( body.StartsAt is not null || body.EndsAt is not null )
            {
                if ( newEndsAt <= newStartsAt )
                {
                    errors[ "endsAt" ] = [ "Конец пары должен быть позже начала" ];
                }
            }

            if ( body.Room is { Length: > 32 } )
            {
                errors[ "room" ] = [ "Аудитория не длиннее 32 символов" ];
            }

            if ( body.Reason is { Length: > 500 } )
            {
                errors[ "reason" ] = [ "Причина не длиннее 500 символов" ];
            }

            if ( body.StartsAt is null && body.EndsAt is null && body.Room is null )
            {
                errors[ "body" ] = [ "Укажите хотя бы одно поле для изменения: startsAt, endsAt или room" ];
            }

            if ( errors.Count > 0 )
            {
                return TypedResults.ValidationProblem( errors );
            }

            // Проверка занятости аудитории: другая пара в той же аудитории в пересекающееся время.
            bool roomBusy = await db.Set<Lesson>()
                .Where( l =>
                    l.Id != lesson.Id &&
                    l.Room == newRoom &&
                    !l.IsCancelled &&
                    l.StartsAt < newEndsAt &&
                    l.EndsAt > newStartsAt )
                .AnyAsync( cancellationToken );

            if ( roomBusy )
            {
                return TypedResults.Problem(
                    title: "Аудитория занята",
                    detail: $"Аудитория {newRoom} занята в выбранное время.",
                    statusCode: StatusCodes.Status409Conflict );
            }

            LessonChangeType changeType = lesson.IsCancelled
                ? LessonChangeType.Cancellation
                : ( body.StartsAt is not null || body.EndsAt is not null )
                    ? LessonChangeType.Reschedule
                    : LessonChangeType.RoomChange;

            LessonChange change = new()
            {
                Id = Guid.NewGuid(),
                LessonId = lesson.Id,
                Type = changeType,
                OldStartsAt = body.StartsAt is not null ? lesson.StartsAt : null,
                NewStartsAt = body.StartsAt,
                OldRoom = body.Room is not null ? lesson.Room : null,
                NewRoom = body.Room?.Trim(),
                Reason = body.Reason?.Trim(),
                ChangedAt = clock.UtcNow,
                ChangedByUserId = userId,
            };

            lesson.StartsAt = newStartsAt;
            lesson.EndsAt = newEndsAt;
            lesson.Room = newRoom;
            lesson.Changes.Add( change );
            await db.SaveChangesAsync( cancellationToken );

            return TypedResults.Ok( new ChangedLessonDto(
                lesson.Id,
                lesson.StartsAt,
                lesson.EndsAt,
                lesson.Room,
                lesson.Type,
                lesson.IsCancelled ) );
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
