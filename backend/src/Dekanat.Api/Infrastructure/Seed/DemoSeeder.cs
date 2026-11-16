using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Profile;
using Dekanat.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dekanat.Api.Infrastructure.Seed
{
    /// <summary>
    /// Пароль демо-учётных записей. Задаётся только переменной окружения Demo__Password (в compose это DEMO_PASSWORD),
    /// в коде и в репозитории его нет. Пустой пароль значит, что демо-данные не создаются.
    /// </summary>
    public sealed class DemoOptions
    {
        public const string Section = "Demo";

        public string? Password { get; set; }
    }

    /// <summary>
    /// Минимальные вымышленные данные, чтобы можно было войти и увидеть профиль. Полный набор данных
    /// (расписание, оценки, заявки) появится в задаче D5-01. Сид идемпотентный: запускать можно сколько угодно раз.
    /// </summary>
    public static class DemoSeeder
    {
        private sealed record DemoStudent( string Login, string FullName, string Group, string GradeBookNumber );

        private sealed record DemoStaff( string Login, string FullName, string Position, string Office );

        private const string FacultyName = "Факультет информатики";

        private static readonly DemoStudent[] Students =
        [
            new( "a.ivanova", "Иванова Анастасия Сергеевна", "ПИ-21", "2026-0123" ),
            new( "m.markov", "Марков Марк Андреевич", "ПИ-21", "2026-0147" ),
        ];

        private static readonly DemoStaff[] StaffMembers =
        [
            new( "o.krylova", "Крылова Ольга Сергеевна", "Специалист деканата", "204" ),
        ];

        public static async Task SeedDemoDataAsync( this WebApplication app )
        {
            string? password = app.Configuration.GetSection( DemoOptions.Section ).Get<DemoOptions>()?.Password;
            if ( string.IsNullOrWhiteSpace( password ) )
            {
                return;
            }

            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            IServiceProvider services = scope.ServiceProvider;
            AppDbContext db = services.GetRequiredService<AppDbContext>();
            UserManager<AppUser> users = services.GetRequiredService<UserManager<AppUser>>();
            ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger( "Dekanat.Api.Infrastructure.Seed" );

            Faculty faculty = await db.Set<Faculty>().SingleOrDefaultAsync( item => item.Name == FacultyName )
                ?? db.Set<Faculty>().Add( new Faculty { Id = Guid.NewGuid(), Name = FacultyName } ).Entity;

            StudyProgram program = await db.Set<StudyProgram>().SingleOrDefaultAsync( item => item.FacultyId == faculty.Id && item.Code == "09.03.04" )
                ?? db.Set<StudyProgram>().Add( new StudyProgram { Id = Guid.NewGuid(), Faculty = faculty, Code = "09.03.04", Name = "Программная инженерия" } ).Entity;

            StudyGroup group = await db.Set<StudyGroup>().SingleOrDefaultAsync( item => item.Name == "ПИ-21" )
                ?? db.Set<StudyGroup>().Add( new StudyGroup { Id = Guid.NewGuid(), Program = program, Name = "ПИ-21", Course = 1 } ).Entity;

            await db.SaveChangesAsync();

            foreach ( DemoStudent student in Students )
            {
                AppUser? user = await EnsureUserAsync( users, student.Login, password, Roles.Student, logger );
                if ( user is not null && !await db.Set<StudentProfile>().AnyAsync( profile => profile.UserId == user.Id ) )
                {
                    db.Set<StudentProfile>().Add( new StudentProfile
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        FullName = student.FullName,
                        GroupId = group.Id,
                        GradeBookNumber = student.GradeBookNumber,
                    } );
                }
            }

            foreach ( DemoStaff staff in StaffMembers )
            {
                AppUser? user = await EnsureUserAsync( users, staff.Login, password, Roles.Staff, logger );
                if ( user is not null && !await db.Set<StaffProfile>().AnyAsync( profile => profile.UserId == user.Id ) )
                {
                    db.Set<StaffProfile>().Add( new StaffProfile
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        FullName = staff.FullName,
                        FacultyId = faculty.Id,
                        Position = staff.Position,
                        Office = staff.Office,
                    } );
                }
            }

            await db.SaveChangesAsync();
        }

        private static async Task<AppUser?> EnsureUserAsync( UserManager<AppUser> users, string login, string password, string role, ILogger logger )
        {
            AppUser? user = await users.FindByNameAsync( login );
            if ( user is not null )
            {
                return user;
            }

            user = new AppUser { Id = Guid.NewGuid(), UserName = login };
            IdentityResult created = await users.CreateAsync( user, password );
            if ( !created.Succeeded )
            {
                // Сам пароль в лог не пишем, только почему он не подошёл.
                logger.LogError(
                    "Демо-учётная запись {Login} не создана: {Errors}",
                    login,
                    string.Join( " ", created.Errors.Select( error => error.Description ) ) );
                return null;
            }

            await users.AddToRoleAsync( user, role );
            return user;
        }
    }
}
