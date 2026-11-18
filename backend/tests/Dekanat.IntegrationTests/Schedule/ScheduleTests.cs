using System.Net;
using System.Text.Json;
using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Profile;
using Dekanat.Api.Features.Schedule;
using Dekanat.Api.Infrastructure.Persistence;
using Dekanat.IntegrationTests.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace Dekanat.IntegrationTests.Schedule
{
    public sealed class ScheduleTests( DekanatApiFactory factory )
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        // Даты далеко в будущем: демо-сид кладёт пары в текущий семестр, а здесь нужны только свои.
        private static readonly DateTime Day = new( 2030, 3, 5, 0, 0, 0, DateTimeKind.Utc );

        /// <summary>Москва UTC+3: 10:40 по Москве это 07:40 UTC.</summary>
        private static DateTime Moscow( int day, int hour, int minute )
        {
            return Day.AddDays( day - 5 ).AddHours( hour - 3 ).AddMinutes( minute );
        }

        [Fact]
        public async Task StudentSeesOnlyOwnGroup()
        {
            TestGroup groupA = await CreateGroupAsync();
            TestGroup groupB = await CreateGroupAsync();
            Lesson lessonA = await AddLessonAsync( groupA, Moscow( 5, 10, 40 ) );
            Lesson lessonB = await AddLessonAsync( groupB, Moscow( 5, 10, 40 ) );
            HttpClient clientA = await SignInAsync( groupA );

            // id чужой группы и студента сервер не читает.
            using JsonDocument schedule = await GetScheduleAsync(
                clientA,
                $"from=2030-03-05&to=2030-03-05&groupId={groupB.Id}&studentId={groupB.StudentUserId}" );

            JsonElement root = schedule.RootElement;
            Assert.Equal( groupA.Name, root.GetProperty( "group" ).GetString() );
            Assert.Equal( [ lessonA.Id ], LessonIds( root ) );
            Assert.DoesNotContain( lessonB.Id.ToString(), root.GetRawText(), StringComparison.Ordinal );
            Assert.DoesNotContain( groupB.Name, root.GetRawText(), StringComparison.Ordinal );

            HttpClient clientB = await SignInAsync( groupB );
            using JsonDocument own = await GetScheduleAsync( clientB, "from=2030-03-05&to=2030-03-05" );
            Assert.Equal( [ lessonB.Id ], LessonIds( own.RootElement ) );
        }

        [Fact]
        public async Task RescheduledLessonComesWithWhatItWas()
        {
            TestGroup group = await CreateGroupAsync();
            Lesson lesson = await AddLessonAsync( group, Moscow( 5, 16, 10 ), room: "305", change: new LessonChange
            {
                Id = Guid.NewGuid(),
                Type = LessonChangeType.Reschedule,
                OldStartsAt = Moscow( 5, 10, 40 ),
                NewStartsAt = Moscow( 5, 16, 10 ),
                OldRoom = "214",
                NewRoom = "305",
                Reason = "Преподаватель на заседании кафедры",
                ChangedAt = Moscow( 4, 15, 20 ),
            } );
            HttpClient client = await SignInAsync( group );

            using JsonDocument schedule = await GetScheduleAsync( client, "from=2030-03-05&to=2030-03-05" );

            JsonElement item = schedule.RootElement.GetProperty( "items" )[ 0 ];
            Assert.Equal( lesson.Id, item.GetProperty( "id" ).GetGuid() );
            Assert.Equal( "2030-03-05T13:10:00Z", item.GetProperty( "startsAt" ).GetString() );
            Assert.Equal( "305", item.GetProperty( "room" ).GetString() );
            Assert.Equal( "Тестов П. И.", item.GetProperty( "teacher" ).GetString() );
            Assert.Equal( "Practice", item.GetProperty( "type" ).GetString() );
            JsonElement change = item.GetProperty( "change" );
            Assert.Equal( "Reschedule", change.GetProperty( "type" ).GetString() );
            Assert.Equal( "2030-03-05T07:40:00Z", change.GetProperty( "oldStartsAt" ).GetString() );
            Assert.Equal( "2030-03-05T13:10:00Z", change.GetProperty( "newStartsAt" ).GetString() );
            Assert.Equal( "214", change.GetProperty( "oldRoom" ).GetString() );
            Assert.Equal( "305", change.GetProperty( "newRoom" ).GetString() );
            Assert.Equal( "Преподаватель на заседании кафедры", change.GetProperty( "reason" ).GetString() );
        }

        [Fact]
        public async Task CancelledLessonStaysInScheduleAndIsMarked()
        {
            TestGroup group = await CreateGroupAsync();
            await AddLessonAsync( group, Moscow( 5, 9, 0 ), cancelled: true, change: new LessonChange
            {
                Id = Guid.NewGuid(),
                Type = LessonChangeType.Cancellation,
                Reason = "Преподаватель на больничном",
                ChangedAt = Moscow( 4, 18, 0 ),
            } );
            await AddLessonAsync( group, Moscow( 5, 10, 40 ) );
            HttpClient client = await SignInAsync( group );

            using JsonDocument schedule = await GetScheduleAsync( client, "from=2030-03-05&to=2030-03-05" );

            JsonElement items = schedule.RootElement.GetProperty( "items" );
            Assert.Equal( 2, items.GetArrayLength() );
            Assert.True( items[ 0 ].GetProperty( "isCancelled" ).GetBoolean() );
            Assert.Equal( "Cancellation", items[ 0 ].GetProperty( "change" ).GetProperty( "type" ).GetString() );
            Assert.False( items[ 1 ].GetProperty( "isCancelled" ).GetBoolean() );
            Assert.Equal( JsonValueKind.Null, items[ 1 ].GetProperty( "change" ).ValueKind );
        }

        [Fact]
        public async Task DaysAreCountedInUniversityTime()
        {
            TestGroup group = await CreateGroupAsync();
            await AddLessonAsync( group, Moscow( 4, 23, 30 ) );
            // 00:30 по Москве 5 марта в UTC ещё 4 марта, но для студента это пара 5-го.
            Lesson earlyMorning = await AddLessonAsync( group, Moscow( 5, 0, 30 ) );
            Lesson lateEvening = await AddLessonAsync( group, Moscow( 5, 22, 0 ) );
            await AddLessonAsync( group, Moscow( 6, 0, 30 ) );
            HttpClient client = await SignInAsync( group );

            using JsonDocument schedule = await GetScheduleAsync( client, "from=2030-03-05&to=2030-03-05" );

            Assert.Equal( "Europe/Moscow", schedule.RootElement.GetProperty( "timeZone" ).GetString() );
            Assert.Equal( [ earlyMorning.Id, lateEvening.Id ], LessonIds( schedule.RootElement ) );
        }

        [Theory]
        [InlineData( "to=2030-03-05", "from", "Укажите первый день периода в формате ГГГГ-ММ-ДД" )]
        [InlineData( "from=2030-03-05", "to", "Укажите последний день периода в формате ГГГГ-ММ-ДД" )]
        [InlineData( "from=2030-03-05&to=2030-03-04", "to", "Последний день не может быть раньше первого" )]
        [InlineData( "from=2030-03-01&to=2030-06-01", "to", "За один раз можно получить не больше 62 дней" )]
        public async Task WrongPeriodGets400WithField( string query, string field, string message )
        {
            HttpClient client = await SignInAsync( await CreateGroupAsync() );

            HttpResponseMessage response = await client.GetAsync( $"/api/schedule?{query}", Token );

            Assert.Equal( HttpStatusCode.BadRequest, response.StatusCode );
            using Problem problem = await Problem.ReadAsync( response );
            Assert.Equal( message, problem.Body.GetProperty( "errors" ).GetProperty( field )[ 0 ].GetString() );
        }

        [Fact]
        public async Task ScheduleRequiresLogin()
        {
            HttpResponseMessage response = await factory.CreateBrowser().GetAsync( "/api/schedule?from=2030-03-05&to=2030-03-05", Token );

            Assert.Equal( HttpStatusCode.Unauthorized, response.StatusCode );
        }

        [Fact]
        public async Task StaffCannotUseStudentSchedule()
        {
            (_, HttpClient client) = await factory.SignedInAsync( Roles.Staff );

            HttpResponseMessage response = await client.GetAsync( "/api/schedule?from=2030-03-05&to=2030-03-05", Token );

            Assert.Equal( HttpStatusCode.Forbidden, response.StatusCode );
        }

        [Fact]
        public async Task StudentWithoutProfileGets404()
        {
            (_, HttpClient client) = await factory.SignedInAsync();

            HttpResponseMessage response = await client.GetAsync( "/api/schedule?from=2030-03-05&to=2030-03-05", Token );

            Assert.Equal( HttpStatusCode.NotFound, response.StatusCode );
        }

        private sealed record TestGroup( Guid Id, string Name, Guid StudentUserId, TestUser Student, Guid TeacherId, Guid DisciplineId );

        private async Task<TestGroup> CreateGroupAsync()
        {
            TestUser student = await TestUsers.CreateAsync( factory.Services );
            string suffix = Guid.NewGuid().ToString( "N" )[ ..8 ];

            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Faculty faculty = new() { Id = Guid.NewGuid(), Name = $"Факультет {suffix}" };
            StudyProgram program = new() { Id = Guid.NewGuid(), Faculty = faculty, Code = "09.03.04", Name = "Программная инженерия" };
            StudyGroup group = new() { Id = Guid.NewGuid(), Program = program, Name = $"Г-{suffix}", Course = 1 };
            Teacher teacher = new() { Id = Guid.NewGuid(), FullName = "Тестов Пётр Ильич", Department = "Кафедра тестов", Position = "Доцент" };
            Discipline discipline = new() { Id = Guid.NewGuid(), Name = $"Тестология {suffix}" };
            db.AddRange( group, teacher, discipline );
            db.Add( new StudentProfile { Id = Guid.NewGuid(), UserId = student.Id, FullName = $"Студентов{suffix} Иван", Group = group, GradeBookNumber = $"S-{suffix}" } );
            await db.SaveChangesAsync( Token );

            return new TestGroup( group.Id, group.Name, student.Id, student, teacher.Id, discipline.Id );
        }

        private async Task<Lesson> AddLessonAsync( TestGroup group, DateTime startsAt, string room = "101", bool cancelled = false, LessonChange? change = null )
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Lesson lesson = new()
            {
                Id = Guid.NewGuid(),
                GroupId = group.Id,
                DisciplineId = group.DisciplineId,
                TeacherId = group.TeacherId,
                StartsAt = startsAt,
                EndsAt = startsAt.AddMinutes( 90 ),
                Room = room,
                Type = LessonType.Practice,
                IsCancelled = cancelled,
            };
            if ( change is not null )
            {
                lesson.Changes.Add( change );
            }

            db.Add( lesson );
            await db.SaveChangesAsync( Token );
            return lesson;
        }

        private async Task<HttpClient> SignInAsync( TestGroup group )
        {
            HttpClient client = factory.CreateBrowser();
            HttpResponseMessage response = await client.LoginAsync( group.Student.Login, group.Student.Password );
            Assert.Equal( HttpStatusCode.OK, response.StatusCode );
            return client;
        }

        private static async Task<JsonDocument> GetScheduleAsync( HttpClient client, string query )
        {
            HttpResponseMessage response = await client.GetAsync( $"/api/schedule?{query}", Token );
            Assert.Equal( HttpStatusCode.OK, response.StatusCode );
            return JsonDocument.Parse( await response.Content.ReadAsStringAsync( Token ) );
        }

        private static Guid[] LessonIds( JsonElement root )
        {
            return [ .. root.GetProperty( "items" ).EnumerateArray().Select( item => item.GetProperty( "id" ).GetGuid() ) ];
        }
    }
}
