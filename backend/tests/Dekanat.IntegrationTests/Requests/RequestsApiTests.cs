using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Profile;
using Dekanat.Api.Features.Requests;
using Dekanat.Api.Infrastructure.Persistence;
using Dekanat.IntegrationTests.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dekanat.IntegrationTests.Requests
{
    public sealed class RequestsApiTests( DekanatApiFactory factory )
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        private sealed record Student( TestUser User, HttpClient Client );

        [Fact]
        public async Task TypesComeWithFieldSchemas()
        {
            Student student = await NewStudentAsync();

            HttpResponseMessage response = await student.Client.GetAsync( "/api/request-types", Token );

            Assert.Equal( HttpStatusCode.OK, response.StatusCode );
            using JsonDocument body = await ReadAsync( response );
            JsonElement[] items = [ .. body.RootElement.GetProperty( "items" ).EnumerateArray() ];
            Assert.Equal( 5, items.Length );
            Assert.Equal( "Справка об обучении", items[ 0 ].GetProperty( "name" ).GetString() );
            Assert.All( items, item =>
            {
                Assert.True( item.GetProperty( "processingDays" ).GetInt32() > 0 );
                Assert.Equal( JsonValueKind.Object, item.GetProperty( "fieldSchema" ).ValueKind );
            } );
        }

        [Fact]
        public async Task StudentSubmitsRequestAndSeesItWithHistory()
        {
            Student student = await NewStudentAsync();

            HttpResponseMessage created = await PostRequestAsync(
                student.Client,
                RequestTypes.StudyCertificateId,
                new { destination = "  В банк  ", copies = 2, delivery = "Deanery", comment = "Срочно" } );

            Assert.Equal( HttpStatusCode.Created, created.StatusCode );
            using JsonDocument body = await ReadAsync( created );
            JsonElement request = body.RootElement;
            Guid id = request.GetProperty( "id" ).GetGuid();
            Assert.Equal( '4', id.ToString()[ 14 ] );
            Assert.Equal( $"/api/requests/{id}", created.Headers.Location!.OriginalString );
            Assert.Equal( "Submitted", request.GetProperty( "status" ).GetString() );
            Assert.Equal( "Справка об обучении", request.GetProperty( "type" ).GetProperty( "name" ).GetString() );
            Assert.Equal( "В банк", request.GetProperty( "fields" ).GetProperty( "destination" ).GetString() );
            Assert.NotEqual( JsonValueKind.Null, request.GetProperty( "estimatedReadyDate" ).ValueKind );
            Assert.True( request.GetProperty( "canCancel" ).GetBoolean() );
            JsonElement history = Assert.Single( request.GetProperty( "history" ).EnumerateArray() );
            Assert.Equal( "Submitted", history.GetProperty( "status" ).GetString() );

            using JsonDocument loaded = await ReadAsync( await student.Client.GetAsync( $"/api/requests/{id}", Token ) );
            Assert.Equal( id, loaded.RootElement.GetProperty( "id" ).GetGuid() );

            using JsonDocument list = await ReadAsync( await student.Client.GetAsync( "/api/requests", Token ) );
            Assert.Equal( [ id ], list.RootElement.GetProperty( "items" ).EnumerateArray().Select( item => item.GetProperty( "id" ).GetGuid() ) );
        }

        [Fact]
        public async Task EveryTypeCanBeSubmitted()
        {
            Student student = await NewStudentAsync();
            (Guid TypeId, object Fields)[] cases =
            [
                (RequestTypes.StudyCertificateId, new { destination = "x", copies = 1, delivery = "Electronic" }),
                (RequestTypes.MilitaryCertificateId, new { destination = "Военкомат района", copies = 1, delivery = "Deanery" }),
                (RequestTypes.SocialCertificateId, new { destination = "Соцзащита", copies = 3, delivery = "Electronic" }),
                (RequestTypes.RetakeApplicationId, new { assessmentId = Guid.NewGuid() }),
                (RequestTypes.AcademicLeaveApplicationId, new { reason = "Health", startDate = "2027-02-01", durationMonths = 6 }),
            ];

            foreach ( (Guid typeId, object fields) in cases )
            {
                HttpResponseMessage response = await PostRequestAsync( student.Client, typeId, fields );

                Assert.Equal( HttpStatusCode.Created, response.StatusCode );
            }

            using JsonDocument list = await ReadAsync( await student.Client.GetAsync( "/api/requests", Token ) );
            Assert.Equal( 5, list.RootElement.GetProperty( "items" ).GetArrayLength() );
        }

        [Fact]
        public async Task FieldsAreValidatedByType()
        {
            Student student = await NewStudentAsync();

            HttpResponseMessage missing = await PostRequestAsync( student.Client, RequestTypes.StudyCertificateId, new { copies = 9 } );
            HttpResponseMessage unknownField = await PostRequestAsync(
                student.Client, RequestTypes.RetakeApplicationId, new { assessmentId = Guid.NewGuid(), status = "Issued" } );
            HttpResponseMessage wrongType = await PostRequestAsync( student.Client, RequestTypes.RetakeApplicationId, new { assessmentId = "abc" } );

            Assert.Equal( HttpStatusCode.BadRequest, missing.StatusCode );
            using JsonDocument missingBody = await ReadAsync( missing );
            JsonElement errors = missingBody.RootElement.GetProperty( "errors" );
            Assert.True( errors.TryGetProperty( "fields.destination", out _ ) );
            Assert.True( errors.TryGetProperty( "fields.copies", out _ ) );
            Assert.True( errors.TryGetProperty( "fields.delivery", out _ ) );

            Assert.Equal( HttpStatusCode.BadRequest, unknownField.StatusCode );
            using JsonDocument unknownBody = await ReadAsync( unknownField );
            Assert.True( unknownBody.RootElement.GetProperty( "errors" ).TryGetProperty( "fields", out _ ) );

            Assert.Equal( HttpStatusCode.BadRequest, wrongType.StatusCode );
            using JsonDocument wrongBody = await ReadAsync( wrongType );
            Assert.True( wrongBody.RootElement.GetProperty( "errors" ).TryGetProperty( "fields.assessmentId", out _ ) );

            // Ни одна заявка не создана.
            using JsonDocument list = await ReadAsync( await student.Client.GetAsync( "/api/requests", Token ) );
            Assert.Equal( 0, list.RootElement.GetProperty( "items" ).GetArrayLength() );
        }

        [Fact]
        public async Task UnknownTypeAndBrokenBodyAreBadRequests()
        {
            Student student = await NewStudentAsync();

            HttpResponseMessage unknownType = await PostRequestAsync( student.Client, Guid.NewGuid(), new { destination = "x" } );
            HttpResponseMessage noFields = await student.Client.PostAsJsonAsync(
                "/api/requests", new { requestTypeId = RequestTypes.StudyCertificateId }, Token );
            HttpResponseMessage brokenJson = await student.Client.PostAsync(
                "/api/requests", new StringContent( "{", System.Text.Encoding.UTF8, "application/json" ), Token );

            Assert.Equal( HttpStatusCode.BadRequest, unknownType.StatusCode );
            using JsonDocument unknownBody = await ReadAsync( unknownType );
            Assert.True( unknownBody.RootElement.GetProperty( "errors" ).TryGetProperty( "requestTypeId", out _ ) );
            Assert.Equal( HttpStatusCode.BadRequest, noFields.StatusCode );
            Assert.Equal( HttpStatusCode.BadRequest, brokenJson.StatusCode );
        }

        [Fact]
        public async Task StudentSeesOnlyOwnRequests()
        {
            Student a = await NewStudentAsync();
            Student b = await NewStudentAsync();
            Guid idA = await SubmitCertificateAsync( a.Client );

            // Чужие id, которые студент B подсовывает в тело и в строку запроса, сервер не читает.
            HttpResponseMessage createdByB = await b.Client.PostAsJsonAsync(
                "/api/requests",
                new
                {
                    requestTypeId = RequestTypes.StudyCertificateId,
                    studentId = a.User.Id,
                    studentProfileId = a.User.Id,
                    fields = new { destination = "Для B", copies = 1, delivery = "Deanery" },
                },
                Token );
            Assert.Equal( HttpStatusCode.Created, createdByB.StatusCode );
            using JsonDocument createdBody = await ReadAsync( createdByB );
            Guid idB = createdBody.RootElement.GetProperty( "id" ).GetGuid();

            using JsonDocument listA = await ReadAsync( await a.Client.GetAsync( $"/api/requests?studentId={b.User.Id}", Token ) );
            using JsonDocument listB = await ReadAsync( await b.Client.GetAsync( $"/api/requests?studentId={a.User.Id}", Token ) );

            Assert.Equal( [ idA ], Ids( listA ) );
            Assert.Equal( [ idB ], Ids( listB ) );
            Assert.DoesNotContain( idA.ToString(), listB.RootElement.GetRawText(), StringComparison.Ordinal );
        }

        [Fact]
        public async Task ListCanBeFilteredByStatusAndComesNewestFirst()
        {
            Student student = await NewStudentAsync();
            Guid first = await SubmitCertificateAsync( student.Client );
            await Task.Delay( 20, Token );
            Guid second = await SubmitCertificateAsync( student.Client );
            HttpResponseMessage cancelled = await student.Client.PostAsync( $"/api/requests/{first}/cancel", null, Token );
            Assert.Equal( HttpStatusCode.OK, cancelled.StatusCode );

            using JsonDocument all = await ReadAsync( await student.Client.GetAsync( "/api/requests", Token ) );
            using JsonDocument submitted = await ReadAsync( await student.Client.GetAsync( "/api/requests?status=Submitted", Token ) );
            using JsonDocument cancelledOnly = await ReadAsync( await student.Client.GetAsync( "/api/requests?status=Cancelled", Token ) );
            HttpResponseMessage badStatus = await student.Client.GetAsync( "/api/requests?status=Nonsense", Token );

            Assert.Equal( [ second, first ], Ids( all ) );
            Assert.Equal( [ second ], Ids( submitted ) );
            Assert.Equal( [ first ], Ids( cancelledOnly ) );
            Assert.Equal( HttpStatusCode.BadRequest, badStatus.StatusCode );
        }

        [Fact]
        public async Task ForeignAndMissingRequestsGiveIdentical404()
        {
            Student owner = await NewStudentAsync();
            Student stranger = await NewStudentAsync();
            Guid foreign = await SubmitCertificateAsync( owner.Client );

            HttpResponseMessage getForeign = await stranger.Client.GetAsync( $"/api/requests/{foreign}", Token );
            HttpResponseMessage getMissing = await stranger.Client.GetAsync( $"/api/requests/{Guid.NewGuid()}", Token );
            HttpResponseMessage cancelForeign = await stranger.Client.PostAsync( $"/api/requests/{foreign}/cancel", null, Token );
            HttpResponseMessage cancelMissing = await stranger.Client.PostAsync( $"/api/requests/{Guid.NewGuid()}/cancel", null, Token );

            foreach ( HttpResponseMessage response in new[] { getForeign, getMissing, cancelForeign, cancelMissing } )
            {
                Assert.Equal( HttpStatusCode.NotFound, response.StatusCode );
            }

            using Problem foreignProblem = await Problem.ReadAsync( getForeign );
            using Problem missingProblem = await Problem.ReadAsync( getMissing );
            using Problem cancelForeignProblem = await Problem.ReadAsync( cancelForeign );
            using Problem cancelMissingProblem = await Problem.ReadAsync( cancelMissing );
            Assert.Equal( Describe( missingProblem ), Describe( foreignProblem ) );
            Assert.Equal( Describe( cancelMissingProblem ), Describe( cancelForeignProblem ) );
            Assert.Equal( Describe( missingProblem ), Describe( cancelForeignProblem ) );

            // Попытка отменить чужую заявку её не тронула.
            using JsonDocument still = await ReadAsync( await owner.Client.GetAsync( $"/api/requests/{foreign}", Token ) );
            Assert.Equal( "Submitted", still.RootElement.GetProperty( "status" ).GetString() );
        }

        [Fact]
        public async Task NonGuidIdIsNotFound()
        {
            Student student = await NewStudentAsync();

            HttpResponseMessage get = await student.Client.GetAsync( "/api/requests/1", Token );
            HttpResponseMessage cancel = await student.Client.PostAsync( "/api/requests/2/cancel", null, Token );

            Assert.Equal( HttpStatusCode.NotFound, get.StatusCode );
            Assert.Equal( HttpStatusCode.NotFound, cancel.StatusCode );
        }

        [Fact]
        public async Task StudentCancelsSubmittedRequestOnce()
        {
            Student student = await NewStudentAsync();
            Guid id = await SubmitCertificateAsync( student.Client );

            HttpResponseMessage cancelled = await student.Client.PostAsync( $"/api/requests/{id}/cancel", null, Token );
            HttpResponseMessage again = await student.Client.PostAsync( $"/api/requests/{id}/cancel", null, Token );

            Assert.Equal( HttpStatusCode.OK, cancelled.StatusCode );
            using JsonDocument body = await ReadAsync( cancelled );
            Assert.Equal( "Cancelled", body.RootElement.GetProperty( "status" ).GetString() );
            Assert.False( body.RootElement.GetProperty( "canCancel" ).GetBoolean() );
            Assert.Equal(
                [ "Submitted", "Cancelled" ],
                body.RootElement.GetProperty( "history" ).EnumerateArray().Select( entry => entry.GetProperty( "status" ).GetString()! ) );

            Assert.Equal( HttpStatusCode.Conflict, again.StatusCode );
            using Problem problem = await Problem.ReadAsync( again );
            Assert.Equal( 409, problem.Status );
        }

        [Fact]
        public async Task CancelAfterTakenIntoWorkIsConflictAndChangesNothing()
        {
            Student student = await NewStudentAsync();
            Guid id = await SubmitCertificateAsync( student.Client );
            TestUser staff = await TestUsers.CreateAsync( factory.Services, Roles.Staff );
            await using ( AsyncServiceScope scope = factory.Services.CreateAsyncScope() )
            {
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Request request = await db.Set<Request>().Include( item => item.History ).SingleAsync( item => item.Id == id, Token );
                RequestWorkflow.Apply( request, new StatusChange( RequestStatus.InProgress, RequestActor.Staff, staff.Id, "Взяли в работу" ), DateTime.UtcNow );
                await db.SaveChangesAsync( Token );
            }

            HttpResponseMessage response = await student.Client.PostAsync( $"/api/requests/{id}/cancel", null, Token );

            Assert.Equal( HttpStatusCode.Conflict, response.StatusCode );
            using JsonDocument details = await ReadAsync( await student.Client.GetAsync( $"/api/requests/{id}", Token ) );
            Assert.Equal( "InProgress", details.RootElement.GetProperty( "status" ).GetString() );
            Assert.False( details.RootElement.GetProperty( "canCancel" ).GetBoolean() );
            Assert.Equal( 2, details.RootElement.GetProperty( "history" ).GetArrayLength() );
            Assert.Equal( "Взяли в работу", details.RootElement.GetProperty( "history" )[ 1 ].GetProperty( "comment" ).GetString() );
        }

        [Fact]
        public async Task StaffAndAnonymousCannotUseStudentApi()
        {
            (_, HttpClient staff) = await factory.SignedInAsync( Roles.Staff );
            HttpClient anonymous = factory.CreateBrowser();
            string id = Guid.NewGuid().ToString();

            foreach ( HttpClient client in new[] { staff, anonymous } )
            {
                HttpStatusCode expected = client == staff ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized;
                Assert.Equal( expected, ( await client.GetAsync( "/api/request-types", Token ) ).StatusCode );
                Assert.Equal( expected, ( await client.GetAsync( "/api/requests", Token ) ).StatusCode );
                Assert.Equal( expected, ( await client.GetAsync( $"/api/requests/{id}", Token ) ).StatusCode );
                Assert.Equal( expected, ( await PostRequestAsync( client, RequestTypes.StudyCertificateId, new { destination = "x", copies = 1, delivery = "Deanery" } ) ).StatusCode );
                Assert.Equal( expected, ( await client.PostAsync( $"/api/requests/{id}/cancel", null, Token ) ).StatusCode );
            }
        }

        [Fact]
        public async Task StudentWithoutProfileGets404()
        {
            (_, HttpClient client) = await factory.SignedInAsync();

            HttpResponseMessage list = await client.GetAsync( "/api/requests", Token );
            HttpResponseMessage create = await PostRequestAsync( client, RequestTypes.StudyCertificateId, new { destination = "x", copies = 1, delivery = "Deanery" } );

            Assert.Equal( HttpStatusCode.NotFound, list.StatusCode );
            Assert.Equal( HttpStatusCode.NotFound, create.StatusCode );
        }

        private static string Describe( Problem problem )
        {
            return $"{problem.Status}|{problem.Title}|{problem.Body.GetProperty( "detail" ).GetString()}";
        }

        private static IEnumerable<Guid> Ids( JsonDocument list )
        {
            return list.RootElement.GetProperty( "items" ).EnumerateArray().Select( item => item.GetProperty( "id" ).GetGuid() );
        }

        private static async Task<JsonDocument> ReadAsync( HttpResponseMessage response )
        {
            return JsonDocument.Parse( await response.Content.ReadAsStringAsync( Token ) );
        }

        private static async Task<HttpResponseMessage> PostRequestAsync( HttpClient client, Guid typeId, object fields )
        {
            return await client.PostAsJsonAsync( "/api/requests", new { requestTypeId = typeId, fields }, Token );
        }

        private static async Task<Guid> SubmitCertificateAsync( HttpClient client )
        {
            HttpResponseMessage response = await PostRequestAsync(
                client, RequestTypes.StudyCertificateId, new { destination = "В банк", copies = 1, delivery = "Deanery" } );
            Assert.Equal( HttpStatusCode.Created, response.StatusCode );
            using JsonDocument body = await ReadAsync( response );
            return body.RootElement.GetProperty( "id" ).GetGuid();
        }

        private async Task<Student> NewStudentAsync()
        {
            TestUser user = await TestUsers.CreateAsync( factory.Services );
            string suffix = Guid.NewGuid().ToString( "N" )[ ..8 ];

            await using ( AsyncServiceScope scope = factory.Services.CreateAsyncScope() )
            {
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Faculty faculty = new() { Id = Guid.NewGuid(), Name = $"Факультет {suffix}" };
                StudyProgram program = new() { Id = Guid.NewGuid(), Faculty = faculty, Code = "09.03.04", Name = "Программная инженерия" };
                StudyGroup group = new() { Id = Guid.NewGuid(), Program = program, Name = $"З-{suffix}", Course = 2 };
                db.Add( new StudentProfile { Id = Guid.NewGuid(), UserId = user.Id, FullName = $"Заявкин{suffix} Иван", Group = group, GradeBookNumber = $"R-{suffix}" } );
                await db.SaveChangesAsync( Token );
            }

            HttpClient client = factory.CreateBrowser();
            HttpResponseMessage login = await client.LoginAsync( user.Login, user.Password );
            Assert.Equal( HttpStatusCode.OK, login.StatusCode );
            return new Student( user, client );
        }
    }
}
