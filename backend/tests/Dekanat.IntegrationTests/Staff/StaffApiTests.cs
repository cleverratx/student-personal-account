using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Profile;
using Dekanat.Api.Features.Requests;
using Dekanat.Api.Features.Schedule;
using Dekanat.Api.Features.Staff;
using Dekanat.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Dekanat.IntegrationTests.Staff
{
    /// <summary>
    /// D8-02: GET /api/staff/requests (фильтры), GET /api/staff/requests/{id},
    /// POST /api/staff/requests/{id}/status, POST /api/staff/announcements,
    /// POST /api/staff/lessons/{id}/change.
    /// </summary>
    public sealed class StaffApiTests( DekanatApiFactory factory )
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        // ── Очередь заявок с фильтрами ─────────────────────────────────────────

        [Fact]
        public async Task QueueFiltersWorkIndependently()
        {
            var ctx = await SetupFacultyAsync();
            Guid typeA = RequestTypes.StudyCertificateId;
            Guid typeB = new Guid( "d7a1c3e5-4b2f-4a6d-9c8e-0f1a2b3c4d03" );
            await SubmitAsync( ctx.StudentA, typeA );
            await SubmitAsync( ctx.StudentB, typeA );
            await SubmitAsync( ctx.StudentA, typeB );

            using JsonDocument byType = await GetQueueAsync( ctx.Staff, $"?type={typeA}" );
            using JsonDocument byGroup = await GetQueueAsync( ctx.Staff, $"?group={Uri.EscapeDataString( ctx.GroupA )}" );
            using JsonDocument bySearch = await GetQueueAsync( ctx.Staff, $"?search={Uri.EscapeDataString( ctx.NameA[ ..4 ] )}" );

            // Два typeA — один от А, один от Б
            Assert.Equal( 2, byType.RootElement.GetProperty( "items" ).GetArrayLength() );
            // Только заявки группы А (typeA + typeB = 2 штуки)
            Assert.Equal( 2, byGroup.RootElement.GetProperty( "items" ).GetArrayLength() );
            // Поиск по ФИО — только заявки студента А (2 штуки)
            Assert.Equal( 2, bySearch.RootElement.GetProperty( "items" ).GetArrayLength() );
        }

        [Fact]
        public async Task SearchIsCaseInsensitive()
        {
            var ctx = await SetupFacultyAsync();
            await SubmitAsync( ctx.StudentA, RequestTypes.StudyCertificateId );

            string lower = ctx.NameA.ToLower();
            string upper = ctx.NameA.ToUpper();

            using JsonDocument qLower = await GetQueueAsync( ctx.Staff, $"?search={Uri.EscapeDataString( lower[ ..5 ] )}" );
            using JsonDocument qUpper = await GetQueueAsync( ctx.Staff, $"?search={Uri.EscapeDataString( upper[ ..5 ] )}" );

            Assert.Equal( 1, qLower.RootElement.GetProperty( "items" ).GetArrayLength() );
            Assert.Equal( 1, qUpper.RootElement.GetProperty( "items" ).GetArrayLength() );
        }

        [Fact]
        public async Task StatusFilterReturnsOnlyMatchingRequests()
        {
            var ctx = await SetupFacultyAsync();
            Guid submittedId = await SubmitAsync( ctx.StudentA, RequestTypes.StudyCertificateId );
            await SubmitAsync( ctx.StudentB, RequestTypes.StudyCertificateId );

            // Переводим одну в InProgress
            await ctx.Staff.PostAsJsonAsync(
                $"/api/staff/requests/{submittedId}/status",
                new { status = "InProgress", comment = "Взята" }, Token );

            using JsonDocument inProg = await GetQueueAsync( ctx.Staff, "?status=InProgress" );
            using JsonDocument submitted = await GetQueueAsync( ctx.Staff, "?status=Submitted" );

            Assert.Equal( 1, inProg.RootElement.GetProperty( "items" ).GetArrayLength() );
            Assert.Equal( 1, submitted.RootElement.GetProperty( "items" ).GetArrayLength() );
        }

        // ── Карточка заявки ───────────────────────────────────────────────────

        [Fact]
        public async Task RequestCardHasAvailableTransitions()
        {
            var ctx = await SetupFacultyAsync();
            Guid id = await SubmitAsync( ctx.StudentA, RequestTypes.StudyCertificateId );

            HttpResponseMessage resp = await ctx.Staff.GetAsync( $"/api/staff/requests/{id}", Token );
            Assert.Equal( HttpStatusCode.OK, resp.StatusCode );
            using JsonDocument doc = await ReadAsync( resp );

            JsonElement transitions = doc.RootElement.GetProperty( "availableTransitions" );
            string[] vals = [ .. transitions.EnumerateArray().Select( e => e.GetString()! ) ];
            Assert.Contains( "InProgress", vals );
            Assert.Contains( "Rejected", vals );
            Assert.DoesNotContain( "Issued", vals );
        }

        [Fact]
        public async Task CardOf_OtherFaculty_Request_Returns404()
        {
            var ctxA = await SetupFacultyAsync();
            var ctxB = await SetupFacultyAsync();
            Guid id = await SubmitAsync( ctxA.StudentA, RequestTypes.StudyCertificateId );

            HttpResponseMessage resp = await ctxB.Staff.GetAsync( $"/api/staff/requests/{id}", Token );
            Assert.Equal( HttpStatusCode.NotFound, resp.StatusCode );
        }

        // ── Смена статуса ─────────────────────────────────────────────────────

        [Fact]
        public async Task FullLifecycle_Submitted_InProgress_Ready_Issued()
        {
            var ctx = await SetupFacultyAsync();
            Guid id = await SubmitAsync( ctx.StudentA, RequestTypes.StudyCertificateId );

            await AssertStatusAsync( ctx.Staff, id, "InProgress",
                new { status = "InProgress", comment = "Взята в работу" } );

            await AssertStatusAsync( ctx.Staff, id, "Ready",
                new { status = "Ready", comment = "Готово", issuePlace = "Деканат 204", issueDeadline = "2099-01-01T10:00:00Z" } );

            using JsonDocument ready = await ReadAsync( await ctx.Staff.GetAsync( $"/api/staff/requests/{id}", Token ) );
            Assert.Equal( "Деканат 204", ready.RootElement.GetProperty( "issuePlace" ).GetString() );
            Assert.Equal( 3, ready.RootElement.GetProperty( "history" ).GetArrayLength() );
            Assert.DoesNotContain( "InProgress", ready.RootElement.GetProperty( "availableTransitions" ).EnumerateArray().Select( e => e.GetString() ) );

            await AssertStatusAsync( ctx.Staff, id, "Issued",
                new { status = "Issued" } );

            // Финальный статус — переходов нет.
            using JsonDocument issued = await ReadAsync( await ctx.Staff.GetAsync( $"/api/staff/requests/{id}", Token ) );
            Assert.Equal( 0, issued.RootElement.GetProperty( "availableTransitions" ).GetArrayLength() );
        }

        [Fact]
        public async Task ReadyWithout_IssuePlace_Returns400()
        {
            var ctx = await SetupFacultyAsync();
            Guid id = await SubmitAsync( ctx.StudentA, RequestTypes.StudyCertificateId );
            await ctx.Staff.PostAsJsonAsync( $"/api/staff/requests/{id}/status",
                new { status = "InProgress" }, Token );

            HttpResponseMessage resp = await ctx.Staff.PostAsJsonAsync(
                $"/api/staff/requests/{id}/status",
                new { status = "Ready", issueDeadline = "2099-01-01T10:00:00Z" }, Token );

            Assert.Equal( HttpStatusCode.BadRequest, resp.StatusCode );
            using JsonDocument doc = await ReadAsync( resp );
            Assert.True( doc.RootElement.GetProperty( "errors" ).TryGetProperty( "issuePlace", out _ ) );
        }

        [Fact]
        public async Task Reject_Without_Comment_Returns400()
        {
            var ctx = await SetupFacultyAsync();
            Guid id = await SubmitAsync( ctx.StudentA, RequestTypes.StudyCertificateId );

            HttpResponseMessage resp = await ctx.Staff.PostAsJsonAsync(
                $"/api/staff/requests/{id}/status",
                new { status = "Rejected" }, Token );

            Assert.Equal( HttpStatusCode.BadRequest, resp.StatusCode );
        }

        [Fact]
        public async Task HistoryGrowsAfterEachTransition()
        {
            var ctx = await SetupFacultyAsync();
            Guid id = await SubmitAsync( ctx.StudentA, RequestTypes.StudyCertificateId );

            async Task<int> HistoryLen()
            {
                using JsonDocument d = await ReadAsync( await ctx.Staff.GetAsync( $"/api/staff/requests/{id}", Token ) );
                return d.RootElement.GetProperty( "history" ).GetArrayLength();
            }

            Assert.Equal( 1, await HistoryLen() );
            await ctx.Staff.PostAsJsonAsync( $"/api/staff/requests/{id}/status", new { status = "InProgress" }, Token );
            Assert.Equal( 2, await HistoryLen() );
            await ctx.Staff.PostAsJsonAsync( $"/api/staff/requests/{id}/status",
                new { status = "Ready", issuePlace = "Деканат 204", issueDeadline = "2099-01-01T10:00:00Z" }, Token );
            Assert.Equal( 3, await HistoryLen() );
        }

        // ── Объявления ────────────────────────────────────────────────────────

        [Fact]
        public async Task CreateAnnouncement_ForGroup_Returns201()
        {
            var ctx = await SetupFacultyAsync();

            var body = new
            {
                title = "Перенос консультации",
                content = "Сегодня в 16:30, аудитория 305.",
                isImportant = true,
                expiresAt = "2099-12-31T23:59:59Z",
                audience = new { type = "Group", groupId = ctx.GroupAId },
            };
            HttpResponseMessage resp = await ctx.Staff.PostAsJsonAsync( "/api/staff/announcements", body, Token );

            Assert.Equal( HttpStatusCode.Created, resp.StatusCode );
            using JsonDocument doc = await ReadAsync( resp );
            Assert.Equal( "Перенос консультации", doc.RootElement.GetProperty( "title" ).GetString() );
            Assert.Equal( "Group", doc.RootElement.GetProperty( "audienceType" ).GetString() );
            Assert.Equal( ctx.GroupAId, doc.RootElement.GetProperty( "groupId" ).GetGuid() );
        }

        [Fact]
        public async Task CreateAnnouncement_ForFaculty_Works()
        {
            var ctx = await SetupFacultyAsync();

            var body = new
            {
                title = "Общее объявление",
                content = "Текст для всего факультета.",
                isImportant = false,
                expiresAt = ( string? )null,
                audience = new { type = "Faculty" },
            };
            HttpResponseMessage resp = await ctx.Staff.PostAsJsonAsync( "/api/staff/announcements", body, Token );

            Assert.Equal( HttpStatusCode.Created, resp.StatusCode );
            using JsonDocument doc = await ReadAsync( resp );
            Assert.Equal( "Faculty", doc.RootElement.GetProperty( "audienceType" ).GetString() );
            Assert.Equal( ctx.FacultyId, doc.RootElement.GetProperty( "facultyId" ).GetGuid() );
        }

        [Fact]
        public async Task CreateAnnouncement_OtherFacultyGroup_Returns400()
        {
            var ctxA = await SetupFacultyAsync();
            var ctxB = await SetupFacultyAsync();

            var body = new
            {
                title = "Объявление",
                content = "Текст.",
                isImportant = false,
                expiresAt = ( string? )null,
                audience = new { type = "Group", groupId = ctxB.GroupAId },   // группа другого факультета
            };
            HttpResponseMessage resp = await ctxA.Staff.PostAsJsonAsync( "/api/staff/announcements", body, Token );

            Assert.Equal( HttpStatusCode.BadRequest, resp.StatusCode );
        }

        // ── Перенос пары ──────────────────────────────────────────────────────

        [Fact]
        public async Task ChangeLesson_TransfersTimeAndRoom()
        {
            Lesson lesson = await CreateLessonAsync();
            HttpClient staff = await NewStaffAsync( lesson.FacultyId );

            var body = new
            {
                startsAt = "2099-05-01T09:00:00Z",
                endsAt = "2099-05-01T10:30:00Z",
                room = "305",
                reason = "Занятость аудитории",
            };
            HttpResponseMessage resp = await staff.PostAsJsonAsync( $"/api/staff/lessons/{lesson.Id}/change", body, Token );

            Assert.Equal( HttpStatusCode.OK, resp.StatusCode );
            using JsonDocument doc = await ReadAsync( resp );
            Assert.Equal( "305", doc.RootElement.GetProperty( "room" ).GetString() );
            Assert.Equal( "2099-05-01T09:00:00Z", doc.RootElement.GetProperty( "startsAt" ).GetString() );
        }

        [Fact]
        public async Task ChangeLesson_OccupiedRoom_Returns409()
        {
            Lesson lessonA = await CreateLessonAsync( room: "214", startsAt: new DateTime( 2099, 5, 2, 8, 0, 0, DateTimeKind.Utc ), endsAt: new DateTime( 2099, 5, 2, 9, 30, 0, DateTimeKind.Utc ) );
            Lesson lessonB = await CreateLessonAsync( room: "400", startsAt: new DateTime( 2099, 5, 2, 8, 30, 0, DateTimeKind.Utc ), endsAt: new DateTime( 2099, 5, 2, 10, 0, 0, DateTimeKind.Utc ) );
            HttpClient staff = await NewStaffAsync( lessonA.FacultyId );

            // Пытаемся перенести пару A в аудиторию 400, которая занята параллельно парой B.
            var body = new { room = "400", reason = "тест" };
            HttpResponseMessage resp = await staff.PostAsJsonAsync( $"/api/staff/lessons/{lessonA.Id}/change", body, Token );

            Assert.Equal( HttpStatusCode.Conflict, resp.StatusCode );
        }

        [Fact]
        public async Task ChangeLesson_MissingAllFields_Returns400()
        {
            Lesson lesson = await CreateLessonAsync();
            HttpClient staff = await NewStaffAsync( lesson.FacultyId );

            HttpResponseMessage resp = await staff.PostAsJsonAsync(
                $"/api/staff/lessons/{lesson.Id}/change",
                new { reason = "причина" },
                Token );

            Assert.Equal( HttpStatusCode.BadRequest, resp.StatusCode );
        }

        // ── Хелперы ───────────────────────────────────────────────────────────

        private sealed record FacultyContext(
            Guid FacultyId,
            HttpClient Staff,
            HttpClient StudentA,
            HttpClient StudentB,
            string NameA,
            string GroupA,
            Guid GroupAId );

        private async Task<FacultyContext> SetupFacultyAsync()
        {
            Guid facultyId = await CreateFacultyAsync();
            (HttpClient studentA, string groupA, Guid groupAId, string nameA) = await NewStudentAsync( facultyId );
            (HttpClient studentB, _, _, _) = await NewStudentAsync( facultyId );
            HttpClient staff = await NewStaffAsync( facultyId );
            return new FacultyContext( facultyId, staff, studentA, studentB, nameA, groupA, groupAId );
        }

        private async Task<Guid> CreateFacultyAsync()
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Faculty faculty = new() { Id = Guid.NewGuid(), Name = $"Ф-{Guid.NewGuid():N}" };
            db.Add( faculty );
            await db.SaveChangesAsync( Token );
            return faculty.Id;
        }

        private async Task<(HttpClient Client, string Group, Guid GroupId, string Name)> NewStudentAsync( Guid facultyId )
        {
            TestUser user = await TestUsers.CreateAsync( factory.Services );
            string suffix = Guid.NewGuid().ToString( "N" )[ ..8 ];
            string group = $"Г-{suffix}";
            string name = $"Студентов{suffix} Иван";

            await using ( AsyncServiceScope scope = factory.Services.CreateAsyncScope() )
            {
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                StudyProgram program = new() { Id = Guid.NewGuid(), FacultyId = facultyId, Code = $"T{suffix}", Name = "Прог" };
                StudyGroup studyGroup = new() { Id = Guid.NewGuid(), Program = program, Name = group, Course = 1 };
                db.Add( new StudentProfile { Id = Guid.NewGuid(), UserId = user.Id, FullName = name, Group = studyGroup, GradeBookNumber = $"S-{suffix}" } );
                await db.SaveChangesAsync( Token );
            }

            HttpClient client = factory.CreateBrowser();
            Assert.Equal( HttpStatusCode.OK, ( await client.LoginAsync( user.Login, user.Password ) ).StatusCode );
            return (client, group, await GroupIdAsync( group ), name);
        }

        private async Task<Guid> GroupIdAsync( string name )
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await db.Set<StudyGroup>().Where( g => g.Name == name ).Select( g => g.Id ).SingleAsync<Guid>( Token );
        }

        private async Task<HttpClient> NewStaffAsync( Guid facultyId )
        {
            TestUser user = await TestUsers.CreateAsync( factory.Services, Roles.Staff );
            await using ( AsyncServiceScope scope = factory.Services.CreateAsyncScope() )
            {
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Add( new StaffProfile { Id = Guid.NewGuid(), UserId = user.Id, FullName = "Тестова О.С.", FacultyId = facultyId, Position = "Специалист", Office = "204" } );
                await db.SaveChangesAsync( Token );
            }
            HttpClient client = factory.CreateBrowser();
            Assert.Equal( HttpStatusCode.OK, ( await client.LoginAsync( user.Login, user.Password ) ).StatusCode );
            return client;
        }

        private sealed record Lesson( Guid Id, Guid FacultyId );

        private async Task<Lesson> CreateLessonAsync( string room = "214", DateTime? startsAt = null, DateTime? endsAt = null )
        {
            DateTime start = startsAt ?? new DateTime( 2099, 5, 1, 7, 40, 0, DateTimeKind.Utc );
            DateTime end = endsAt ?? new DateTime( 2099, 5, 1, 9, 10, 0, DateTimeKind.Utc );

            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Guid facultyId = await CreateFacultyAsync();
            StudyProgram program = new() { Id = Guid.NewGuid(), FacultyId = facultyId, Code = $"L{Guid.NewGuid().ToString( "N" )[ ..6 ]}", Name = "Прог" };
            StudyGroup group = new() { Id = Guid.NewGuid(), Program = program, Name = $"ЛГ-{Guid.NewGuid().ToString( "N" )[ ..4 ]}", Course = 1 };
            Teacher teacher = new() { Id = Guid.NewGuid(), FullName = "Преп П.П.", Department = "Каф", Position = "Доц" };
            Discipline disc = new() { Id = Guid.NewGuid(), Name = "Дисциплина" };
            var lesson = new Dekanat.Api.Features.Schedule.Lesson
            {
                Id = Guid.NewGuid(),
                Group = group,
                Discipline = disc,
                Teacher = teacher,
                StartsAt = start,
                EndsAt = end,
                Room = room,
                Type = LessonType.Lecture,
            };
            db.Add( lesson );
            await db.SaveChangesAsync( Token );
            return new Lesson( lesson.Id, facultyId );
        }

        private static async Task<Guid> SubmitAsync( HttpClient student, Guid typeId )
        {
            var body = typeId == RequestTypes.StudyCertificateId
                ? ( object )new { requestTypeId = typeId, fields = new { destination = "В банк", copies = 1, delivery = "Deanery" } }
                : new { requestTypeId = typeId, fields = new { destination = "Соцзащита", copies = 1, delivery = "Deanery" } };
            HttpResponseMessage resp = await student.PostAsJsonAsync( "/api/requests", body, Token );
            Assert.Equal( HttpStatusCode.Created, resp.StatusCode );
            using JsonDocument doc = await ReadAsync( resp );
            return doc.RootElement.GetProperty( "id" ).GetGuid();
        }

        private static async Task AssertStatusAsync( HttpClient staff, Guid id, string expectedStatus, object body )
        {
            HttpResponseMessage resp = await staff.PostAsJsonAsync( $"/api/staff/requests/{id}/status", body, Token );
            Assert.Equal( HttpStatusCode.OK, resp.StatusCode );
            using JsonDocument doc = await ReadAsync( resp );
            Assert.Equal( expectedStatus, doc.RootElement.GetProperty( "status" ).GetString() );
        }

        private static async Task<JsonDocument> GetQueueAsync( HttpClient staff, string query = "" )
        {
            HttpResponseMessage resp = await staff.GetAsync( $"/api/staff/requests{query}", Token );
            Assert.Equal( HttpStatusCode.OK, resp.StatusCode );
            return await ReadAsync( resp );
        }

        private static async Task<JsonDocument> ReadAsync( HttpResponseMessage response )
        {
            return JsonDocument.Parse( await response.Content.ReadAsStringAsync( Token ) );
        }
    }
}
