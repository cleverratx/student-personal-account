using System.Security.Claims;
using Dekanat.Api.Features.Profile;
using Dekanat.Api.Features.Requests;
using Dekanat.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dekanat.Api.Features.Staff
{
    /// <summary>
    /// Границы данных сотрудника. Факультет берём из его профиля по сессии, из запроса его не читаем:
    /// сотрудник видит только заявки студентов своего факультета.
    /// </summary>
    public static class StaffScope
    {
        /// <summary>Факультет сотрудника из сессии. Пусто, если профиль сотрудника ещё не заведён.</summary>
        public static async Task<Guid?> FacultyIdAsync( ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken )
        {
            Guid userId = Guid.Parse( principal.FindFirstValue( ClaimTypes.NameIdentifier )! );
            return await db.Set<StaffProfile>()
                .Where( profile => profile.UserId == userId )
                .Select( profile => ( Guid? )profile.FacultyId )
                .SingleOrDefaultAsync( cancellationToken );
        }

        /// <summary>Заявки студентов одного факультета. Факультет студента определяется через его группу и направление.</summary>
        public static IQueryable<Request> InFaculty( this IQueryable<Request> requests, Guid facultyId )
        {
            return requests.Where( request => request.StudentProfile.Group.Program.FacultyId == facultyId );
        }

        public static IResult ProfileNotFound()
        {
            return TypedResults.Problem(
                title: "Профиль не найден",
                detail: "Для этой учётной записи профиль сотрудника ещё не заведён.",
                statusCode: StatusCodes.Status404NotFound );
        }
    }
}
