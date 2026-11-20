using System.Security.Claims;
using System.Text.Json;
using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Profile;
using Dekanat.Api.Infrastructure.Persistence;
using Dekanat.Api.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace Dekanat.Api.Features.Requests
{
    /// <param name="Id">Id типа заявки.</param>
    /// <param name="Name">Название, например «Справка в военкомат».</param>
    public sealed record RequestTypeRefDto( Guid Id, string Name );

    /// <param name="Id">Id типа заявки.</param>
    /// <param name="Name">Название.</param>
    /// <param name="Description">Пояснение для карточки выбора.</param>
    /// <param name="ProcessingDays">Срок изготовления в рабочих днях.</param>
    /// <param name="FieldSchema">Поля заявки: объект, где ключ это имя поля, а значение описывает его (type, required, title, options, min, max, maxLength).</param>
    public sealed record RequestTypeDto( Guid Id, string Name, string Description, int ProcessingDays, JsonElement FieldSchema );

    /// <param name="Items">Типы заявок.</param>
    public sealed record RequestTypesResponse( IReadOnlyList<RequestTypeDto> Items );

    /// <param name="RequestTypeId">Id типа из списка типов заявок.</param>
    /// <param name="Fields">Значения полей по схеме типа: объект, где ключ это имя поля.</param>
    public sealed record CreateRequestBody( Guid RequestTypeId, JsonElement Fields );

    /// <param name="Id">Id заявки, UUID.</param>
    /// <param name="Type">Тип заявки.</param>
    /// <param name="Status">Статус сейчас.</param>
    /// <param name="CreatedAt">Когда подана, UTC.</param>
    /// <param name="EstimatedReadyDate">Примерная дата готовности по времени вуза.</param>
    /// <param name="IssuePlace">Где забрать. Есть, когда заявка готова.</param>
    /// <param name="IssueDeadline">До какого времени забрать, UTC. Есть, когда заявка готова.</param>
    /// <param name="Fields">Значения полей, как их заполнил студент.</param>
    /// <param name="CanCancel">Можно ли отменить заявку сейчас. Правило одно, оно на сервере.</param>
    public sealed record RequestDto(
        Guid Id,
        RequestTypeRefDto Type,
        RequestStatus Status,
        DateTime CreatedAt,
        DateOnly? EstimatedReadyDate,
        string? IssuePlace,
        DateTime? IssueDeadline,
        JsonElement Fields,
        bool CanCancel );

    /// <param name="Status">В какой статус перевели.</param>
    /// <param name="Comment">Комментарий деканата. У отклонённой заявки это причина отказа.</param>
    /// <param name="ChangedAt">Когда, UTC.</param>
    public sealed record RequestHistoryEntryDto( RequestStatus Status, string? Comment, DateTime ChangedAt );

    /// <param name="Id">Id заявки, UUID.</param>
    /// <param name="Type">Тип заявки.</param>
    /// <param name="Status">Статус сейчас.</param>
    /// <param name="CreatedAt">Когда подана, UTC.</param>
    /// <param name="EstimatedReadyDate">Примерная дата готовности по времени вуза.</param>
    /// <param name="IssuePlace">Где забрать. Есть, когда заявка готова.</param>
    /// <param name="IssueDeadline">До какого времени забрать, UTC. Есть, когда заявка готова.</param>
    /// <param name="Fields">Значения полей, как их заполнил студент.</param>
    /// <param name="CanCancel">Можно ли отменить заявку сейчас.</param>
    /// <param name="History">История статусов от первого к последнему.</param>
    public sealed record RequestDetailsDto(
        Guid Id,
        RequestTypeRefDto Type,
        RequestStatus Status,
        DateTime CreatedAt,
        DateOnly? EstimatedReadyDate,
        string? IssuePlace,
        DateTime? IssueDeadline,
        JsonElement Fields,
        bool CanCancel,
        IReadOnlyList<RequestHistoryEntryDto> History );

    /// <param name="Items">Заявки студента, новые сверху.</param>
    public sealed record RequestsResponse( IReadOnlyList<RequestDto> Items );

    public static class RequestEndpoints
    {
        public static RouteGroupBuilder MapRequests( this RouteGroupBuilder api )
        {
            api.MapGet( "/request-types", GetTypesAsync )
                .WithTags( "Requests" )
                .WithName( "GetRequestTypes" )
                .WithSummary( "Типы заявок и поля каждого типа" )
                .RequireAuthorization( AuthorizationPolicies.Student )
                .Produces<RequestTypesResponse>()
                .ProducesProblem( StatusCodes.Status401Unauthorized )
                .ProducesProblem( StatusCodes.Status403Forbidden );

            api.MapGet( "/requests", GetRequestsAsync )
                .WithTags( "Requests" )
                .WithName( "GetRequests" )
                .WithSummary( "Заявки того, кто вошёл. Чьи заявки, сервер берёт только из сессии" )
                .RequireAuthorization( AuthorizationPolicies.Student )
                .Produces<RequestsResponse>()
                .ProducesValidationProblem()
                .ProducesProblem( StatusCodes.Status401Unauthorized )
                .ProducesProblem( StatusCodes.Status403Forbidden )
                .ProducesProblem( StatusCodes.Status404NotFound );

            api.MapPost( "/requests", CreateRequestAsync )
                .WithTags( "Requests" )
                .WithName( "CreateRequest" )
                .WithSummary( "Подаёт заявку от имени того, кто вошёл. Поля проверяются по схеме типа" )
                .RequireAuthorization( AuthorizationPolicies.Student )
                .Produces<RequestDetailsDto>( StatusCodes.Status201Created )
                .ProducesValidationProblem()
                .ProducesProblem( StatusCodes.Status401Unauthorized )
                .ProducesProblem( StatusCodes.Status403Forbidden )
                .ProducesProblem( StatusCodes.Status404NotFound );

            api.MapGet( "/requests/{id:guid}", GetRequestAsync )
                .WithTags( "Requests" )
                .WithName( "GetRequest" )
                .WithSummary( "Заявка с историей статусов. Чужая и несуществующая заявки одинаково дают 404" )
                .RequireAuthorization( AuthorizationPolicies.Student )
                .Produces<RequestDetailsDto>()
                .ProducesProblem( StatusCodes.Status401Unauthorized )
                .ProducesProblem( StatusCodes.Status403Forbidden )
                .ProducesProblem( StatusCodes.Status404NotFound );

            api.MapPost( "/requests/{id:guid}/cancel", CancelRequestAsync )
                .WithTags( "Requests" )
                .WithName( "CancelRequest" )
                .WithSummary( "Отменяет заявку, пока она в статусе «Подана». Дальше её ведёт деканат" )
                .RequireAuthorization( AuthorizationPolicies.Student )
                .Produces<RequestDetailsDto>()
                .ProducesProblem( StatusCodes.Status401Unauthorized )
                .ProducesProblem( StatusCodes.Status403Forbidden )
                .ProducesProblem( StatusCodes.Status404NotFound )
                .ProducesProblem( StatusCodes.Status409Conflict );

            return api;
        }

        private static async Task<IResult> GetTypesAsync( AppDbContext db, CancellationToken cancellationToken )
        {
            // Порядок по id совпадает с порядком в справочнике: сначала справки, потом заявления.
            List<RequestType> types = await db.Set<RequestType>().AsNoTracking().OrderBy( type => type.Id ).ToListAsync( cancellationToken );

            return TypedResults.Ok( new RequestTypesResponse(
                [ .. types.Select( type => new RequestTypeDto( type.Id, type.Name, type.Description, type.ProcessingDays, ParseJson( type.FieldSchema ) ) ) ] ) );
        }

        /// <param name="status">Только заявки в этом статусе. Без параметра приходят все.</param>
        private static async Task<IResult> GetRequestsAsync(
            RequestStatus? status,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken cancellationToken )
        {
            Guid? profileId = await StudentProfileIdAsync( principal, db, cancellationToken );
            if ( profileId is null )
            {
                return ProfileNotFound();
            }

            // Фильтр по студенту стоит в самом запросе: чужие заявки в выборку не попадают.
            IQueryable<Request> query = db.Set<Request>().Where( request => request.StudentProfileId == profileId );
            if ( status is not null )
            {
                RequestStatus wanted = status.Value;
                query = query.Where( request => request.CurrentStatus == wanted );
            }

            List<Request> requests = await query
                .Include( request => request.Type )
                .OrderByDescending( request => request.CreatedAt )
                .AsNoTracking()
                .ToListAsync( cancellationToken );

            return TypedResults.Ok( new RequestsResponse( [ .. requests.Select( request => ToDto( request, request.Type.Name ) ) ] ) );
        }

        private static async Task<IResult> CreateRequestAsync(
            CreateRequestBody body,
            ClaimsPrincipal principal,
            AppDbContext db,
            UniversityClock clock,
            CancellationToken cancellationToken )
        {
            Guid? profileId = await StudentProfileIdAsync( principal, db, cancellationToken );
            if ( profileId is null )
            {
                return ProfileNotFound();
            }

            RequestType? type = await db.Set<RequestType>().AsNoTracking()
                .SingleOrDefaultAsync( item => item.Id == body.RequestTypeId, cancellationToken );
            if ( type is null )
            {
                return TypedResults.ValidationProblem( new Dictionary<string, string[]> { [ "requestTypeId" ] = [ "Выберите тип заявки из списка" ] } );
            }

            FieldsValidation fields = RequestFieldsValidator.Validate( body.Fields, type.FieldSchema );
            if ( fields.Errors.Count > 0 )
            {
                return TypedResults.ValidationProblem( fields.Errors );
            }

            Request request = RequestWorkflow.Submit( profileId.Value, UserId( principal ), type, fields.Json!, clock.UtcNow, clock.Today );
            db.Add( request );
            await db.SaveChangesAsync( cancellationToken );

            return TypedResults.Created( $"/api/requests/{request.Id}", ToDetails( request, type.Name ) );
        }

        private static async Task<IResult> GetRequestAsync(
            Guid id,
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken cancellationToken )
        {
            Guid? profileId = await StudentProfileIdAsync( principal, db, cancellationToken );

            // Чужая заявка и несуществующая проходят один и тот же путь: запрос ищет заявку только среди своих.
            Request? request = profileId is null
                ? null
                : await db.Set<Request>()
                    .Include( item => item.Type )
                    .Include( item => item.History )
                    .AsNoTracking()
                    .SingleOrDefaultAsync( item => item.Id == id && item.StudentProfileId == profileId, cancellationToken );

            return request is null ? RequestNotFound() : TypedResults.Ok( ToDetails( request, request.Type.Name ) );
        }

        private static async Task<IResult> CancelRequestAsync(
            Guid id,
            ClaimsPrincipal principal,
            AppDbContext db,
            UniversityClock clock,
            CancellationToken cancellationToken )
        {
            Guid? profileId = await StudentProfileIdAsync( principal, db, cancellationToken );

            Request? request = profileId is null
                ? null
                : await db.Set<Request>()
                    .Include( item => item.Type )
                    .Include( item => item.History )
                    .SingleOrDefaultAsync( item => item.Id == id && item.StudentProfileId == profileId, cancellationToken );
            if ( request is null )
            {
                return RequestNotFound();
            }

            TransitionResult result = RequestWorkflow.Apply(
                request,
                new StatusChange( RequestStatus.Cancelled, RequestActor.Student, UserId( principal ) ),
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
                // Деканат успел взять заявку в работу между чтением и сохранением.
                return TypedResults.Problem(
                    title: "Статус заявки сейчас нельзя изменить",
                    detail: "Заявка только что изменилась. Обновите страницу и проверьте её статус.",
                    statusCode: StatusCodes.Status409Conflict );
            }

            return TypedResults.Ok( ToDetails( request, request.Type.Name ) );
        }

        private static Guid UserId( ClaimsPrincipal principal )
        {
            return Guid.Parse( principal.FindFirstValue( ClaimTypes.NameIdentifier )! );
        }

        /// <summary>Профиль студента из сессии. Других источников у студента нет: id из запроса не читаем.</summary>
        private static async Task<Guid?> StudentProfileIdAsync( ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken )
        {
            Guid userId = UserId( principal );
            return await db.Set<StudentProfile>()
                .Where( profile => profile.UserId == userId )
                .Select( profile => ( Guid? )profile.Id )
                .SingleOrDefaultAsync( cancellationToken );
        }

        private static RequestDto ToDto( Request request, string typeName )
        {
            return new RequestDto(
                request.Id,
                new RequestTypeRefDto( request.RequestTypeId, typeName ),
                request.CurrentStatus,
                request.CreatedAt,
                request.EstimatedReadyDate,
                request.IssuePlace,
                request.IssueDeadline,
                ParseJson( request.Fields ),
                RequestWorkflow.IsAllowed( request.CurrentStatus, RequestStatus.Cancelled, RequestActor.Student ) );
        }

        private static RequestDetailsDto ToDetails( Request request, string typeName )
        {
            RequestDto dto = ToDto( request, typeName );
            return new RequestDetailsDto(
                dto.Id,
                dto.Type,
                dto.Status,
                dto.CreatedAt,
                dto.EstimatedReadyDate,
                dto.IssuePlace,
                dto.IssueDeadline,
                dto.Fields,
                dto.CanCancel,
                [ .. request.History.OrderBy( entry => entry.ChangedAt ).Select( entry => new RequestHistoryEntryDto( entry.Status, entry.Comment, entry.ChangedAt ) ) ] );
        }

        internal static JsonElement ParseJson( string json )
        {
            using JsonDocument document = JsonDocument.Parse( json );
            return document.RootElement.Clone();
        }

        private static IResult RequestNotFound()
        {
            return TypedResults.Problem(
                title: "Заявка не найдена",
                detail: "Заявка не найдена или недоступна.",
                statusCode: StatusCodes.Status404NotFound );
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
