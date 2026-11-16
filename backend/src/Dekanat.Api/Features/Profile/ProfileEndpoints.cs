using System.Security.Claims;
using Dekanat.Api.Features.Auth;
using Dekanat.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dekanat.Api.Features.Profile
{
    /// <param name="Faculty">Факультет.</param>
    /// <param name="ProgramCode">Код направления, например 09.03.04.</param>
    /// <param name="ProgramName">Название направления.</param>
    /// <param name="Course">Курс.</param>
    /// <param name="Group">Группа.</param>
    /// <param name="GradeBookNumber">Номер зачётной книжки.</param>
    public sealed record StudentProfileDto( string Faculty, string ProgramCode, string ProgramName, int Course, string Group, string GradeBookNumber );

    /// <param name="Faculty">Факультет, за который отвечает сотрудник.</param>
    /// <param name="Position">Должность.</param>
    /// <param name="Office">Кабинет, если указан.</param>
    public sealed record StaffProfileDto( string Faculty, string Position, string? Office );

    /// <param name="Role">Роль: по ней заполнено либо student, либо staff.</param>
    /// <param name="Login">Логин.</param>
    /// <param name="FullName">ФИО полностью.</param>
    /// <param name="Initials">Две буквы для аватара: имя и фамилия.</param>
    /// <param name="Student">Учебные данные, только у студента.</param>
    /// <param name="Staff">Данные сотрудника, только у сотрудника.</param>
    public sealed record ProfileResponse(
        UserRole Role,
        string Login,
        string FullName,
        string Initials,
        StudentProfileDto? Student,
        StaffProfileDto? Staff );

    public static class ProfileEndpoints
    {
        public static RouteGroupBuilder MapProfile( this RouteGroupBuilder api )
        {
            api.MapGet( "/me/profile", GetProfileAsync )
                .WithTags( "Profile" )
                .WithName( "GetMyProfile" )
                .WithSummary( "Профиль того, кто вошёл. Чей профиль, сервер берёт только из сессии" )
                .Produces<ProfileResponse>()
                .ProducesProblem( StatusCodes.Status401Unauthorized )
                .ProducesProblem( StatusCodes.Status404NotFound );

            return api;
        }

        private static async Task<IResult> GetProfileAsync( ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken )
        {
            // Никаких id из запроса: только тот, кто вошёл.
            Guid userId = Guid.Parse( principal.FindFirstValue( ClaimTypes.NameIdentifier )! );
            string login = principal.Identity!.Name!;

            if ( principal.IsInRole( Roles.Staff ) )
            {
                ProfileResponse? staff = await db.Set<StaffProfile>()
                    .Where( profile => profile.UserId == userId )
                    .Select( profile => new ProfileResponse(
                        UserRole.Staff,
                        login,
                        profile.FullName,
                        "",
                        null,
                        new StaffProfileDto( profile.Faculty.Name, profile.Position, profile.Office ) ) )
                    .SingleOrDefaultAsync( cancellationToken );
                return staff is null ? NotFound() : TypedResults.Ok( staff with { Initials = InitialsOf( staff.FullName ) } );
            }

            ProfileResponse? student = await db.Set<StudentProfile>()
                .Where( profile => profile.UserId == userId )
                .Select( profile => new ProfileResponse(
                    UserRole.Student,
                    login,
                    profile.FullName,
                    "",
                    new StudentProfileDto(
                        profile.Group.Program.Faculty.Name,
                        profile.Group.Program.Code,
                        profile.Group.Program.Name,
                        profile.Group.Course,
                        profile.Group.Name,
                        profile.GradeBookNumber ),
                    null ) )
                .SingleOrDefaultAsync( cancellationToken );
            return student is null ? NotFound() : TypedResults.Ok( student with { Initials = InitialsOf( student.FullName ) } );
        }

        /// <summary>
        /// «Иванова Анастасия Сергеевна» → «АИ»: сначала имя, потом фамилия, как на аватарах в макете.
        /// </summary>
        public static string InitialsOf( string fullName )
        {
            string[] parts = fullName.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
            return parts.Length switch
            {
                0 => "",
                1 => parts[ 0 ][ ..1 ].ToUpperInvariant(),
                _ => $"{parts[ 1 ][ 0 ]}{parts[ 0 ][ 0 ]}".ToUpperInvariant(),
            };
        }

        private static IResult NotFound()
        {
            return TypedResults.Problem(
                title: "Профиль не найден",
                detail: "Для этой учётной записи профиль ещё не заведён. Обратитесь в деканат.",
                statusCode: StatusCodes.Status404NotFound );
        }
    }
}
