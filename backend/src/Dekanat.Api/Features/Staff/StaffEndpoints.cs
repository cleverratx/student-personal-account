using System.Security.Claims;
using System.Text.Json;
using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Requests;
using Dekanat.Api.Infrastructure.Persistence;
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

            return api;
        }

        private static async Task<IResult> GetRequestsAsync( ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken )
        {
            Guid? facultyId = await StaffScope.FacultyIdAsync( principal, db, cancellationToken );
            if ( facultyId is null )
            {
                return StaffScope.ProfileNotFound();
            }

            // Фильтр по факультету стоит в самом запросе: заявки других факультетов в выборку не попадают.
            var rows = await db.Set<Request>()
                .AsNoTracking()
                .InFaculty( facultyId.Value )
                .OrderBy( request => request.CreatedAt )
                .ThenBy( request => request.Id )
                .Select( request => new
                {
                    request.Id,
                    StudentId = request.StudentProfileId,
                    request.StudentProfile.FullName,
                    Group = request.StudentProfile.Group.Name,
                    request.RequestTypeId,
                    TypeName = request.Type.Name,
                    request.CurrentStatus,
                    request.CreatedAt,
                    request.EstimatedReadyDate,
                    request.Fields,
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
    }
}
