using Dekanat.Api.Features.Profile;
using Dekanat.Api.Features.Requests;
using Dekanat.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dekanat.IntegrationTests.Requests
{
    /// <summary>
    /// Модель заявок в настоящей базе: справочник типов, сохранение заявки с историей, ограничения и блокировка версии.
    /// </summary>
    public sealed class RequestModelTests( DekanatApiFactory factory )
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Fact]
        public async Task RequestTypesAreSeededByMigration()
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            List<RequestType> types = await db.Set<RequestType>().AsNoTracking().ToListAsync( Token );

            Assert.Equal( RequestTypes.All.Select( type => type.Id ).Order(), types.Select( type => type.Id ).Order() );
            Assert.All( types, type => Assert.StartsWith( "{", type.FieldSchema, StringComparison.Ordinal ) );
        }

        [Fact]
        public async Task RequestWithHistoryIsSavedAndLoaded()
        {
            (Guid profileId, Guid userId) = await CreateStudentAsync();
            Request request = RequestWorkflow.Submit(
                profileId, userId, RequestTypes.All[ 1 ], """{"destination":"Военкомат","copies":1,"delivery":"Deanery"}""",
                DateTime.UtcNow, DateOnly.FromDateTime( DateTime.UtcNow ) );

            await using ( AsyncServiceScope scope = factory.Services.CreateAsyncScope() )
            {
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Add( request );
                await db.SaveChangesAsync( Token );
            }

            await using AsyncServiceScope readScope = factory.Services.CreateAsyncScope();
            AppDbContext read = readScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Request saved = await read.Set<Request>().Include( item => item.History ).SingleAsync( item => item.Id == request.Id, Token );

            Assert.Equal( RequestStatus.Submitted, saved.CurrentStatus );
            Assert.Contains( "\"destination\"", saved.Fields, StringComparison.Ordinal );
            Assert.Equal( request.EstimatedReadyDate, saved.EstimatedReadyDate );
            RequestStatusHistory entry = Assert.Single( saved.History );
            Assert.Equal( userId, entry.ChangedByUserId );
        }

        [Fact]
        public async Task SecondChangeOfStaleRequestFailsOnVersion()
        {
            (Guid profileId, Guid userId) = await CreateStudentAsync();
            Request request = RequestWorkflow.Submit( profileId, userId, RequestTypes.All[ 0 ], "{}", DateTime.UtcNow, DateOnly.FromDateTime( DateTime.UtcNow ) );
            await using ( AsyncServiceScope scope = factory.Services.CreateAsyncScope() )
            {
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Add( request );
                await db.SaveChangesAsync( Token );
            }

            // Студент и сотрудник открыли одну и ту же заявку, пока она «Подана».
            await using AsyncServiceScope studentScope = factory.Services.CreateAsyncScope();
            await using AsyncServiceScope staffScope = factory.Services.CreateAsyncScope();
            AppDbContext studentDb = studentScope.ServiceProvider.GetRequiredService<AppDbContext>();
            AppDbContext staffDb = staffScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Request seenByStudent = await studentDb.Set<Request>().SingleAsync( item => item.Id == request.Id, Token );
            Request seenByStaff = await staffDb.Set<Request>().SingleAsync( item => item.Id == request.Id, Token );

            Assert.IsType<TransitionResult.Success>( RequestWorkflow.Apply(
                seenByStaff, new StatusChange( RequestStatus.InProgress, RequestActor.Staff, userId ), DateTime.UtcNow ) );
            await staffDb.SaveChangesAsync( Token );

            Assert.IsType<TransitionResult.Success>( RequestWorkflow.Apply(
                seenByStudent, new StatusChange( RequestStatus.Cancelled, RequestActor.Student, userId ), DateTime.UtcNow ) );
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>( () => studentDb.SaveChangesAsync( Token ) );
        }

        [Fact]
        public async Task DatabaseRejectsUnknownStatus()
        {
            (Guid profileId, Guid userId) = await CreateStudentAsync();
            Request request = RequestWorkflow.Submit( profileId, userId, RequestTypes.All[ 0 ], "{}", DateTime.UtcNow, DateOnly.FromDateTime( DateTime.UtcNow ) );
            request.CurrentStatus = ( RequestStatus ) 99;

            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Add( request );

            DbUpdateException error = await Assert.ThrowsAsync<DbUpdateException>( () => db.SaveChangesAsync( Token ) );
            Assert.IsType<PostgresException>( error.InnerException );
        }

        private async Task<(Guid ProfileId, Guid UserId)> CreateStudentAsync()
        {
            TestUser user = await TestUsers.CreateAsync( factory.Services );
            string suffix = Guid.NewGuid().ToString( "N" )[ ..8 ];
            Guid profileId = Guid.NewGuid();

            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Faculty faculty = new() { Id = Guid.NewGuid(), Name = $"Факультет {suffix}" };
            StudyProgram program = new() { Id = Guid.NewGuid(), Faculty = faculty, Code = "09.03.04", Name = "Программная инженерия" };
            StudyGroup group = new() { Id = Guid.NewGuid(), Program = program, Name = $"З-{suffix}", Course = 2 };
            db.Add( new StudentProfile { Id = profileId, UserId = user.Id, FullName = "Заявкина Анна Игоревна", Group = group, GradeBookNumber = $"Z-{suffix}" } );
            await db.SaveChangesAsync( Token );

            return (profileId, user.Id);
        }
    }
}
