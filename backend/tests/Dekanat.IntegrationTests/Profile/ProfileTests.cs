using System.Net;
using System.Text.Json;
using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Profile;
using Dekanat.Api.Infrastructure.Persistence;
using Dekanat.IntegrationTests.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace Dekanat.IntegrationTests.Profile
{
    public sealed class ProfileTests( DekanatApiFactory factory )
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Fact]
        public async Task StudentGetsOwnProfile()
        {
            (TestUser user, string fullName, string group) = await CreateStudentAsync();
            HttpClient client = factory.CreateBrowser();
            await client.LoginAsync( user.Login, user.Password );

            using JsonDocument profile = await GetProfileAsync( client );

            JsonElement root = profile.RootElement;
            Assert.Equal( "Student", root.GetProperty( "role" ).GetString() );
            Assert.Equal( fullName, root.GetProperty( "fullName" ).GetString() );
            Assert.Equal( "ТС", root.GetProperty( "initials" ).GetString() );
            JsonElement student = root.GetProperty( "student" );
            Assert.Equal( group, student.GetProperty( "group" ).GetString() );
            Assert.Equal( 2, student.GetProperty( "course" ).GetInt32() );
            Assert.Equal( "09.03.04", student.GetProperty( "programCode" ).GetString() );
            Assert.Equal( JsonValueKind.Null, root.GetProperty( "staff" ).ValueKind );
        }

        [Fact]
        public async Task OtherStudentProfileCannotBeRequestedInAnyWay()
        {
            (TestUser studentA, string nameA, _) = await CreateStudentAsync();
            (TestUser studentB, string nameB, _) = await CreateStudentAsync();
            HttpClient clientA = factory.CreateBrowser();
            await clientA.LoginAsync( studentA.Login, studentA.Password );

            // Подставляем id студента Б всеми способами: сервер их не читает и отдаёт профиль А.
            using JsonDocument withQuery = await GetProfileAsync(
                clientA,
                $"?userId={studentB.Id}&studentId={studentB.Id}&id={studentB.Id}" );
            Assert.Equal( nameA, withQuery.RootElement.GetProperty( "fullName" ).GetString() );
            Assert.DoesNotContain( nameB, withQuery.RootElement.GetRawText(), StringComparison.Ordinal );

            HttpResponseMessage byPath = await clientA.GetAsync( $"/api/me/profile/{studentB.Id}", Token );
            Assert.Equal( HttpStatusCode.NotFound, byPath.StatusCode );
            Assert.DoesNotContain( nameB, await byPath.Content.ReadAsStringAsync( Token ), StringComparison.Ordinal );

            HttpClient clientB = factory.CreateBrowser();
            await clientB.LoginAsync( studentB.Login, studentB.Password );
            using JsonDocument own = await GetProfileAsync( clientB );
            Assert.Equal( nameB, own.RootElement.GetProperty( "fullName" ).GetString() );
        }

        [Fact]
        public async Task StaffGetsStaffProfile()
        {
            TestUser user = await TestUsers.CreateAsync( factory.Services, Roles.Staff );
            await using ( AsyncServiceScope scope = factory.Services.CreateAsyncScope() )
            {
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Faculty faculty = new() { Id = Guid.NewGuid(), Name = $"Факультет {Guid.NewGuid():N}" };
                db.Add( faculty );
                db.Add( new StaffProfile { Id = Guid.NewGuid(), UserId = user.Id, FullName = "Тестова Ольга Сергеевна", Faculty = faculty, Position = "Специалист деканата", Office = "204" } );
                await db.SaveChangesAsync( Token );
            }

            HttpClient client = factory.CreateBrowser();
            await client.LoginAsync( user.Login, user.Password );
            using JsonDocument profile = await GetProfileAsync( client );

            Assert.Equal( "Staff", profile.RootElement.GetProperty( "role" ).GetString() );
            Assert.Equal( "204", profile.RootElement.GetProperty( "staff" ).GetProperty( "office" ).GetString() );
            Assert.Equal( JsonValueKind.Null, profile.RootElement.GetProperty( "student" ).ValueKind );
        }

        [Fact]
        public async Task ProfileRequiresLogin()
        {
            HttpResponseMessage response = await factory.CreateBrowser().GetAsync( "/api/me/profile", Token );

            Assert.Equal( HttpStatusCode.Unauthorized, response.StatusCode );
        }

        [Fact]
        public async Task AccountWithoutProfileGets404()
        {
            (_, HttpClient client) = await factory.SignedInAsync();

            HttpResponseMessage response = await client.GetAsync( "/api/me/profile", Token );

            Assert.Equal( HttpStatusCode.NotFound, response.StatusCode );
            using Problem problem = await Problem.ReadAsync( response );
            Assert.Equal( "Профиль не найден", problem.Title );
        }

        private async Task<(TestUser User, string FullName, string Group)> CreateStudentAsync()
        {
            TestUser user = await TestUsers.CreateAsync( factory.Services );
            string suffix = Guid.NewGuid().ToString( "N" )[ ..8 ];
            string fullName = $"Студентова{suffix} Татьяна Петровна";
            string groupName = $"Т-{suffix}";

            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Faculty faculty = new() { Id = Guid.NewGuid(), Name = $"Факультет {suffix}" };
            StudyProgram program = new() { Id = Guid.NewGuid(), Faculty = faculty, Code = "09.03.04", Name = "Программная инженерия" };
            StudyGroup group = new() { Id = Guid.NewGuid(), Program = program, Name = groupName, Course = 2 };
            db.Add( new StudentProfile { Id = Guid.NewGuid(), UserId = user.Id, FullName = fullName, Group = group, GradeBookNumber = $"T-{suffix}" } );
            await db.SaveChangesAsync( Token );

            return (user, fullName, groupName);
        }

        private static async Task<JsonDocument> GetProfileAsync( HttpClient client, string query = "" )
        {
            HttpResponseMessage response = await client.GetAsync( $"/api/me/profile{query}", Token );
            Assert.Equal( HttpStatusCode.OK, response.StatusCode );
            return JsonDocument.Parse( await response.Content.ReadAsStringAsync( Token ) );
        }
    }
}
