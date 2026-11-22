using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Grades;
using Dekanat.Api.Features.Profile;
using Dekanat.Api.Features.Schedule;
using Dekanat.Api.Infrastructure.Persistence;
using Dekanat.Api.Infrastructure.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Dekanat.Api.Infrastructure.Seed.DemoData;

namespace Dekanat.Api.Infrastructure.Seed
{
    /// <summary>
    /// Пароль демо-учётных записей. Задаётся только переменной окружения Demo__Password (в compose это DEMO_PASSWORD),
    /// в коде и в репозитории его нет. Пустой пароль значит, что демо-режим выключен и данные не создаются.
    /// </summary>
    public sealed class DemoOptions
    {
        public const string Section = "Demo";

        public string? Password { get; set; }
    }

    /// <summary>
    /// Вымышленные данные из <see cref="DemoData"/>: факультеты, группы, студенты, сотрудник, преподаватели,
    /// дисциплины и расписание текущего семестра с переносами. Сид идемпотентный: каждую запись ищет перед
    /// созданием, а расписание группы создаёт, только если у группы ещё нет ни одной пары.
    /// </summary>
    public static class DemoSeeder
    {
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
            UniversityClock clock = services.GetRequiredService<UniversityClock>();
            ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger( "Dekanat.Api.Infrastructure.Seed" );

            Dictionary<string, Faculty> faculties = await EnsureFacultiesAsync( db );
            Dictionary<string, StudyGroup> groups = await EnsureGroupsAsync( db, faculties );
            Guid? staffUserId = await EnsurePeopleAsync( db, users, password, faculties, groups, logger );
            Dictionary<string, Teacher> teachers = await EnsureTeachersAsync( db );
            Dictionary<string, Discipline> disciplines = await EnsureDisciplinesAsync( db );

            foreach ( StudyGroup group in groups.Values )
            {
                if ( await db.Set<Lesson>().AnyAsync( lesson => lesson.GroupId == group.Id ) )
                {
                    continue;
                }

                List<(DateOnly Date, Lesson Lesson)> lessons = BuildSemester( group, teachers, disciplines, clock );
                ApplyChanges( group.Name, lessons, clock, staffUserId );
                db.Set<Lesson>().AddRange( lessons.Select( item => item.Lesson ) );

                // Одно сохранение на группу: расписание и его изменения появляются вместе или не появляются вовсе.
                await db.SaveChangesAsync();
                logger.LogInformation( "Демо-расписание группы {Group}: {Count} пар", group.Name, lessons.Count );
            }

            // Оценки и расписание сессии — после расписания, чтобы дисциплины уже были в базе.
            await GradesSeed.SeedGradesAsync( db, clock, logger );
        }

        private static async Task<Dictionary<string, Faculty>> EnsureFacultiesAsync( AppDbContext db )
        {
            Dictionary<string, Faculty> result = [];
            foreach ( string name in Faculties )
            {
                result[ name ] = await db.Set<Faculty>().SingleOrDefaultAsync( item => item.Name == name )
                    ?? db.Set<Faculty>().Add( new Faculty { Id = Guid.NewGuid(), Name = name } ).Entity;
            }

            await db.SaveChangesAsync();
            return result;
        }

        private static async Task<Dictionary<string, StudyGroup>> EnsureGroupsAsync( AppDbContext db, Dictionary<string, Faculty> faculties )
        {
            Dictionary<string, StudyProgram> programs = [];
            foreach ( DemoProgram demo in Programs )
            {
                Faculty faculty = faculties[ demo.Faculty ];
                programs[ demo.Code ] = await db.Set<StudyProgram>().SingleOrDefaultAsync( item => item.FacultyId == faculty.Id && item.Code == demo.Code )
                    ?? db.Set<StudyProgram>().Add( new StudyProgram { Id = Guid.NewGuid(), Faculty = faculty, Code = demo.Code, Name = demo.Name } ).Entity;
            }

            Dictionary<string, StudyGroup> groups = [];
            foreach ( DemoGroup demo in Groups )
            {
                groups[ demo.Name ] = await db.Set<StudyGroup>().SingleOrDefaultAsync( item => item.Name == demo.Name )
                    ?? db.Set<StudyGroup>().Add( new StudyGroup { Id = Guid.NewGuid(), Program = programs[ demo.ProgramCode ], Name = demo.Name, Course = demo.Course } ).Entity;
            }

            await db.SaveChangesAsync();
            return groups;
        }

        /// <summary>Учётные записи и профили. Возвращает id сотрудника: он записан автором изменений расписания.</summary>
        private static async Task<Guid?> EnsurePeopleAsync(
            AppDbContext db,
            UserManager<AppUser> users,
            string password,
            Dictionary<string, Faculty> faculties,
            Dictionary<string, StudyGroup> groups,
            ILogger logger )
        {
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
                        GroupId = groups[ student.Group ].Id,
                        GradeBookNumber = student.GradeBookNumber,
                    } );
                }
            }

            Guid? staffUserId = null;
            foreach ( DemoStaff staff in StaffMembers )
            {
                AppUser? user = await EnsureUserAsync( users, staff.Login, password, Roles.Staff, logger );
                staffUserId ??= user?.Id;
                if ( user is not null && !await db.Set<StaffProfile>().AnyAsync( profile => profile.UserId == user.Id ) )
                {
                    db.Set<StaffProfile>().Add( new StaffProfile
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        FullName = staff.FullName,
                        FacultyId = faculties[ staff.Faculty ].Id,
                        Position = staff.Position,
                        Office = staff.Office,
                    } );
                }
            }

            await db.SaveChangesAsync();
            return staffUserId;
        }

        private static async Task<Dictionary<string, Teacher>> EnsureTeachersAsync( AppDbContext db )
        {
            Dictionary<string, Teacher> result = [];
            foreach ( DemoTeacher demo in Teachers )
            {
                result[ demo.Surname ] = await db.Set<Teacher>().FirstOrDefaultAsync( item => item.FullName == demo.FullName )
                    ?? db.Set<Teacher>().Add( new Teacher { Id = Guid.NewGuid(), FullName = demo.FullName, Department = demo.Department, Position = demo.Position } ).Entity;
            }

            await db.SaveChangesAsync();
            return result;
        }

        private static async Task<Dictionary<string, Discipline>> EnsureDisciplinesAsync( AppDbContext db )
        {
            Dictionary<string, Discipline> result = [];
            foreach ( string name in DemoData.Disciplines )
            {
                result[ name ] = await db.Set<Discipline>().SingleOrDefaultAsync( item => item.Name == name )
                    ?? db.Set<Discipline>().Add( new Discipline { Id = Guid.NewGuid(), Name = name } ).Entity;
            }

            await db.SaveChangesAsync();
            return result;
        }

        private static List<(DateOnly Date, Lesson Lesson)> BuildSemester(
            StudyGroup group,
            Dictionary<string, Teacher> teachers,
            Dictionary<string, Discipline> disciplines,
            UniversityClock clock )
        {
            DemoLesson[] template = [ .. Week.Where( entry => entry.Group == group.Name ) ];
            Semester semester = Semester.Containing( clock.Today );

            return
            [
                .. SemesterPlan.Expand( semester, template, entry => entry.Day, entry => entry.Weeks )
                    .Select( item =>
                    {
                        (TimeOnly start, TimeOnly end) = SemesterPlan.Slots[ item.Entry.Slot ];
                        return (item.Date, new Lesson
                        {
                            Id = Guid.NewGuid(),
                            GroupId = group.Id,
                            DisciplineId = disciplines[ item.Entry.Discipline ].Id,
                            TeacherId = teachers[ item.Entry.Teacher ].Id,
                            StartsAt = clock.ToUtc( item.Date, start ),
                            EndsAt = clock.ToUtc( item.Date, end ),
                            Room = item.Entry.Room,
                            Type = item.Entry.Type,
                        });
                    } ),
            ];
        }

        /// <summary>Изменения из <see cref="DemoData.Changes"/> в ближайшие учебные дни после сегодняшнего.</summary>
        private static void ApplyChanges( string group, List<(DateOnly Date, Lesson Lesson)> lessons, UniversityClock clock, Guid? staffUserId )
        {
            DateOnly today = clock.Today;
            // Порядок пар в дне запоминаем до изменений: перенос не должен сдвинуть, какая пара «вторая».
            DateOnly[] studyDays = [ .. lessons.Select( item => item.Date ).Where( date => date > today ).Distinct().Order() ];
            Dictionary<DateOnly, Lesson[]> byDay = lessons
                .GroupBy( item => item.Date )
                .ToDictionary( day => day.Key, day => day.Select( item => item.Lesson ).OrderBy( lesson => lesson.StartsAt ).ToArray() );

            foreach ( DemoChange demo in Changes.Where( change => change.Group == group ) )
            {
                if ( demo.StudyDay >= studyDays.Length )
                {
                    continue;
                }

                DateOnly date = studyDays[ demo.StudyDay ];
                Lesson[] day = byDay[ date ];
                Lesson? lesson = demo.Lesson switch
                {
                    Pick.First => day[ 0 ],
                    Pick.Second => day.Length > 1 ? day[ 1 ] : null,
                    _ => day[ ^1 ],
                };
                if ( lesson is null )
                {
                    continue;
                }

                LessonChange change = new()
                {
                    Id = Guid.NewGuid(),
                    LessonId = lesson.Id,
                    Type = demo.Type,
                    Reason = demo.Reason,
                    ChangedAt = clock.UtcNow.AddHours( -demo.HoursAgo ),
                    ChangedByUserId = staffUserId,
                };

                if ( demo.NewSlot is int slot )
                {
                    (TimeOnly start, TimeOnly end) = SemesterPlan.Slots[ slot ];
                    change.OldStartsAt = lesson.StartsAt;
                    lesson.StartsAt = clock.ToUtc( date, start );
                    lesson.EndsAt = clock.ToUtc( date, end );
                    change.NewStartsAt = lesson.StartsAt;
                }

                if ( demo.NewRoom is string room )
                {
                    change.OldRoom = lesson.Room;
                    change.NewRoom = room;
                    lesson.Room = room;
                }

                if ( demo.Type == LessonChangeType.Cancellation )
                {
                    lesson.IsCancelled = true;
                }

                lesson.Changes.Add( change );
            }
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
