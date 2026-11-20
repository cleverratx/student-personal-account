using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Profile;
using Dekanat.Api.Features.Requests;
using Dekanat.Api.Infrastructure.Persistence;
using Dekanat.IntegrationTests.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dekanat.IntegrationTests.Staff
{
    /// <summary>
    /// Роли и доступ: /api/staff только для сотрудника, студенческие эндпоинты только для студента,
    /// а сотрудник видит заявки одного своего факультета.
    /// </summary>
    public sealed class StaffAccessTests( DekanatApiFactory factory )
    {
        private const string StaffQueue = "/api/staff/requests";

        private static CancellationToken Token => TestContext.Current.CancellationToken;

        private sealed record Submitted( Guid RequestId, string StudentName, string Group );

        [Fact]
        public async Task AnonymousGets401()
        {
            HttpResponseMessage response = await factory.CreateBrowser().GetAsync( StaffQueue, Token );

            Assert.Equal( HttpStatusCode.Unauthorized, response.StatusCode );
        }

        [Fact]
        public async Task StudentGets403OnStaffApiAndSeesNoData()
        {
            Guid facultyId = await CreateFacultyAsync();
            (HttpClient student, _) = await NewStudentAsync( facultyId );
            Submitted own = await SubmitAsync( student );

            HttpResponseMessage response = await student.GetAsync( StaffQueue, Token );

            Assert.Equal( HttpStatusCode.Forbidden, response.StatusCode );
            using Problem problem = await Problem.ReadAsync( response );
            Assert.Equal( 403, problem.Status );
            Assert.DoesNotContain( own.RequestId.ToString(), problem.Body.GetRawText(), StringComparison.Ordinal );
            Assert.DoesNotContain( own.StudentName, problem.Body.GetRawText(), StringComparison.Ordinal );
        }

        [Fact]
        public async Task StaffWithoutProfileGets404()
        {
            (_, HttpClient client) = await factory.SignedInAsync( Roles.Staff );

            HttpResponseMessage response = await client.GetAsync( StaffQueue, Token );

            Assert.Equal( HttpStatusCode.NotFound, response.StatusCode );
        }

        [Fact]
        public async Task StaffSeesOnlyRequestsOfOwnFaculty()
        {
            Guid facultyA = await CreateFacultyAsync();
            Guid facultyB = await CreateFacultyAsync();
            (HttpClient studentA1, _) = await NewStudentAsync( facultyA );
            (HttpClient studentA2, _) = await NewStudentAsync( facultyA );
            (HttpClient studentB, _) = await NewStudentAsync( facultyB );
            Submitted a1 = await SubmitAsync( studentA1 );
            await Task.Delay( 20, Token );
            Submitted a2 = await SubmitAsync( studentA2 );
            Submitted b = await SubmitAsync( studentB );
            HttpClient staffA = await NewStaffAsync( facultyA );
            HttpClient staffA2 = await NewStaffAsync( facultyA );
            HttpClient staffB = await NewStaffAsync( facultyB );

            HttpResponseMessage responseA = await staffA.GetAsync( StaffQueue, Token );
            using JsonDocument queueA = await ReadAsync( responseA );
            using JsonDocument queueA2 = await ReadAsync( await staffA2.GetAsync( StaffQueue, Token ) );
            using JsonDocument queueB = await ReadAsync( await staffB.GetAsync( StaffQueue, Token ) );

            Assert.Equal( HttpStatusCode.OK, responseA.StatusCode );
            // Старые заявки сверху, заявок другого факультета нет ни в списке, ни в тексте ответа.
            Assert.Equal( [ a1.RequestId, a2.RequestId ], Ids( queueA ) );
            Assert.Equal( [ a1.RequestId, a2.RequestId ], Ids( queueA2 ) );
            Assert.DoesNotContain( b.RequestId.ToString(), queueA.RootElement.GetRawText(), StringComparison.Ordinal );
            Assert.DoesNotContain( b.StudentName, queueA.RootElement.GetRawText(), StringComparison.Ordinal );
            Assert.Equal( [ b.RequestId ], Ids( queueB ) );
            Assert.DoesNotContain( a1.StudentName, queueB.RootElement.GetRawText(), StringComparison.Ordinal );
            Assert.DoesNotContain( a2.StudentName, queueB.RootElement.GetRawText(), StringComparison.Ordinal );
        }

        [Fact]
        public async Task StaffCannotAskForAnotherFacultyInTheQuery()
        {
            Guid facultyA = await CreateFacultyAsync();
            Guid facultyB = await CreateFacultyAsync();
            (HttpClient studentA, _) = await NewStudentAsync( facultyA );
            (HttpClient studentB, _) = await NewStudentAsync( facultyB );
            Submitted a = await SubmitAsync( studentA );
            Submitted b = await SubmitAsync( studentB );
            HttpClient staffA = await NewStaffAsync( facultyA );

            // Факультет из запроса сервер не читает: ответ тот же, что без параметров.
            using JsonDocument queue = await ReadAsync( await staffA.GetAsync( $"{StaffQueue}?facultyId={facultyB}&FacultyId={facultyB}", Token ) );

            Assert.Equal( [ a.RequestId ], Ids( queue ) );
            Assert.DoesNotContain( b.RequestId.ToString(), queue.RootElement.GetRawText(), StringComparison.Ordinal );
        }

        [Fact]
        public async Task QueueShowsStudentTypeAndStatus()
        {
            Guid facultyId = await CreateFacultyAsync();
            (HttpClient student, string group) = await NewStudentAsync( facultyId );
            Submitted submitted = await SubmitAsync( student );
            HttpClient staff = await NewStaffAsync( facultyId );

            using JsonDocument queue = await ReadAsync( await staff.GetAsync( StaffQueue, Token ) );

            JsonElement item = Assert.Single( queue.RootElement.GetProperty( "items" ).EnumerateArray() );
            Assert.Equal( submitted.RequestId, item.GetProperty( "id" ).GetGuid() );
            Assert.Equal( submitted.StudentName, item.GetProperty( "student" ).GetProperty( "fullName" ).GetString() );
            Assert.Equal( group, item.GetProperty( "student" ).GetProperty( "group" ).GetString() );
            Assert.Equal( "Справка об обучении", item.GetProperty( "type" ).GetProperty( "name" ).GetString() );
            Assert.Equal( "Submitted", item.GetProperty( "status" ).GetString() );
            Assert.Equal( "В банк", item.GetProperty( "fields" ).GetProperty( "destination" ).GetString() );
        }

        [Fact]
        public async Task RolesCannotUseEachOthersApi()
        {
            Guid facultyId = await CreateFacultyAsync();
            (HttpClient student, _) = await NewStudentAsync( facultyId );
            HttpClient staff = await NewStaffAsync( facultyId );
            HttpClient anonymous = factory.CreateBrowser();
            string[] studentOnly = [ "/api/schedule?from=2030-03-05&to=2030-03-05", "/api/requests", "/api/request-types" ];

            foreach ( string url in studentOnly )
            {
                Assert.Equal( HttpStatusCode.Forbidden, ( await staff.GetAsync( url, Token ) ).StatusCode );
                Assert.Equal( HttpStatusCode.Unauthorized, ( await anonymous.GetAsync( url, Token ) ).StatusCode );
                Assert.NotEqual( HttpStatusCode.Forbidden, ( await student.GetAsync( url, Token ) ).StatusCode );
            }

            Assert.Equal( HttpStatusCode.Forbidden, ( await student.GetAsync( StaffQueue, Token ) ).StatusCode );
            Assert.Equal( HttpStatusCode.Unauthorized, ( await anonymous.GetAsync( StaffQueue, Token ) ).StatusCode );
            Assert.Equal( HttpStatusCode.OK, ( await staff.GetAsync( StaffQueue, Token ) ).StatusCode );
        }

        [Fact]
        public async Task StudentAndStaffPoliciesRequireTheirRole()
        {
            IAuthorizationPolicyProvider policies = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

            AuthorizationPolicy? student = await policies.GetPolicyAsync( AuthorizationPolicies.Student );
            AuthorizationPolicy? staff = await policies.GetPolicyAsync( AuthorizationPolicies.Staff );

            Assert.NotNull( student );
            Assert.NotNull( staff );
            Assert.Equal( [ Roles.Student ], Assert.Single( student.Requirements.OfType<RolesAuthorizationRequirement>() ).AllowedRoles );
            Assert.Equal( [ Roles.Staff ], Assert.Single( staff.Requirements.OfType<RolesAuthorizationRequirement>() ).AllowedRoles );
        }

        private static IEnumerable<Guid> Ids( JsonDocument queue )
        {
            return queue.RootElement.GetProperty( "items" ).EnumerateArray().Select( item => item.GetProperty( "id" ).GetGuid() );
        }

        private static async Task<JsonDocument> ReadAsync( HttpResponseMessage response )
        {
            return JsonDocument.Parse( await response.Content.ReadAsStringAsync( Token ) );
        }

        private async Task<Guid> CreateFacultyAsync()
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Faculty faculty = new() { Id = Guid.NewGuid(), Name = $"Факультет {Guid.NewGuid():N}" };
            db.Add( faculty );
            await db.SaveChangesAsync( Token );
            return faculty.Id;
        }

        private async Task<(HttpClient Client, string Group)> NewStudentAsync( Guid facultyId )
        {
            TestUser user = await TestUsers.CreateAsync( factory.Services );
            string suffix = Guid.NewGuid().ToString( "N" )[ ..8 ];
            string group = $"Д-{suffix}";

            await using ( AsyncServiceScope scope = factory.Services.CreateAsyncScope() )
            {
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                StudyProgram program = new() { Id = Guid.NewGuid(), FacultyId = facultyId, Code = $"T{suffix}", Name = "Программная инженерия" };
                StudyGroup studyGroup = new() { Id = Guid.NewGuid(), Program = program, Name = group, Course = 2 };
                db.Add( new StudentProfile { Id = Guid.NewGuid(), UserId = user.Id, FullName = $"Очередин{suffix} Пётр", Group = studyGroup, GradeBookNumber = $"S-{suffix}" } );
                await db.SaveChangesAsync( Token );
            }

            return (await SignInAsync( user ), group);
        }

        private async Task<HttpClient> NewStaffAsync( Guid facultyId )
        {
            TestUser user = await TestUsers.CreateAsync( factory.Services, Roles.Staff );

            await using ( AsyncServiceScope scope = factory.Services.CreateAsyncScope() )
            {
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Add( new StaffProfile { Id = Guid.NewGuid(), UserId = user.Id, FullName = "Тестова Ольга Сергеевна", FacultyId = facultyId, Position = "Специалист деканата", Office = "204" } );
                await db.SaveChangesAsync( Token );
            }

            return await SignInAsync( user );
        }

        private async Task<HttpClient> SignInAsync( TestUser user )
        {
            HttpClient client = factory.CreateBrowser();
            HttpResponseMessage login = await client.LoginAsync( user.Login, user.Password );
            Assert.Equal( HttpStatusCode.OK, login.StatusCode );
            return client;
        }

        /// <summary>Студент подаёт справку через настоящий API, как в приложении.</summary>
        private static async Task<Submitted> SubmitAsync( HttpClient student )
        {
            HttpResponseMessage response = await student.PostAsJsonAsync(
                "/api/requests",
                new { requestTypeId = RequestTypes.StudyCertificateId, fields = new { destination = "В банк", copies = 1, delivery = "Deanery" } },
                Token );
            Assert.Equal( HttpStatusCode.Created, response.StatusCode );
            using JsonDocument body = await ReadAsync( response );
            Guid id = body.RootElement.GetProperty( "id" ).GetGuid();

            using JsonDocument profile = await ReadAsync( await student.GetAsync( "/api/me/profile", Token ) );
            return new Submitted(
                id,
                profile.RootElement.GetProperty( "fullName" ).GetString()!,
                profile.RootElement.GetProperty( "student" ).GetProperty( "group" ).GetString()! );
        }
    }
}
