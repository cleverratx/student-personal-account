using System.Net;
using System.Text.Json;
using Dekanat.Api.Features.Profile;
using Dekanat.Api.Features.Schedule;
using Dekanat.Api.Infrastructure.Persistence;
using Dekanat.Api.Infrastructure.Seed;
using Dekanat.Api.Infrastructure.Time;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dekanat.IntegrationTests.Seed
{
    /// <summary>git add .
    /// Демо-сид на общей тестовой базе. Все проверки сида в одном тесте: два сида параллельно на одной базе
    /// мешали бы друг другу.
    /// </summary>
    public sealed class DemoSeedTests( DekanatApiFactory factory )
    {
        private const string Password = "Demo-seed-2026";

        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Fact]
        public async Task DemoSeedRunsOnceAndOnlyWithPassword()
        {
            // Считаем только пары демо-групп: другие тесты параллельно добавляют свои пары в ту же базу.
            string[] seededGroups = [ .. DemoData.Groups.Select( group => group.Name ) ];
            int[] lessonCounts = new int[ 2 ];
            for ( int run = 0; run < 2; run++ )
            {
                await using WebApplicationFactory<Program> demo = factory.WithWebHostBuilder( builder =>
                    builder.UseSetting( $"{DemoOptions.Section}:Password", Password ) );
                HttpClient client = demo.CreateBrowser();
                HttpResponseMessage login = await client.LoginAsync( "a.ivanova", Password );
                Assert.Equal( HttpStatusCode.OK, login.StatusCode );
                lessonCounts[ run ] = await CountAsync( db => db.Set<Lesson>().CountAsync( lesson => seededGroups.Contains( lesson.Group.Name ), Token ) );
            }

            // Второй запуск ничего не добавил.
            Assert.Equal( lessonCounts[ 0 ], lessonCounts[ 1 ] );

            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            string[] demoGroups = [ .. DemoData.Groups.Select( group => group.Name ) ];
            Assert.Equal( 1, await db.Users.CountAsync( user => user.UserName == "a.ivanova", Token ) );
            Assert.Equal( 3, await db.Set<StudyGroup>().CountAsync( group => demoGroups.Contains( group.Name ), Token ) );
            Assert.Equal( DemoData.Students.Length, await db.Set<StudentProfile>().CountAsync( profile => demoGroups.Contains( profile.Group.Name ), Token ) );
            Assert.InRange( DemoData.Teachers.Length, 12, 15 );
            Assert.InRange( DemoData.Disciplines.Length, 10, 12 );

            foreach ( string group in demoGroups )
            {
                Assert.True( await db.Set<Lesson>().CountAsync( lesson => lesson.Group.Name == group, Token ) > 100, $"У {group} нет расписания на семестр" );
            }

            Assert.Equal( 3, await ChangesOf( db, "ПИ-21" ).CountAsync( Token ) );
            Assert.Equal( 0, await ChangesOf( db, "БИ-31" ).CountAsync( Token ) );
        }

        [Fact]
        public async Task SeededStudentSeesRescheduleInComingDays()
        {
            await using WebApplicationFactory<Program> demo = factory.WithWebHostBuilder( builder =>
                builder.UseSetting( $"{DemoOptions.Section}:Password", Password ) );
            HttpClient client = demo.CreateBrowser();
            Assert.Equal( HttpStatusCode.OK, ( await client.LoginAsync( "a.ivanova", Password ) ).StatusCode );
            // Сид ставит изменения на ближайшие учебные дни. Летом это сентябрь, поэтому период берём от первого изменения.
            DateTime firstChanged;
            await using ( AsyncServiceScope scope = demo.Services.CreateAsyncScope() )
            {
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                firstChanged = await db.Set<Lesson>()
                    .Where( lesson => lesson.Group.Name == "ПИ-21" && lesson.Changes.Any() )
                    .MinAsync( lesson => lesson.StartsAt, Token );
            }

            UniversityClock clock = demo.Services.GetRequiredService<UniversityClock>();
            DateOnly from = DateOnly.FromDateTime( TimeZoneInfo.ConvertTimeFromUtc( firstChanged, clock.Zone ) );

            HttpResponseMessage response = await client.GetAsync( $"/api/schedule?from={from:yyyy-MM-dd}&to={from.AddDays( 14 ):yyyy-MM-dd}", Token );

            Assert.Equal( HttpStatusCode.OK, response.StatusCode );
            using JsonDocument schedule = JsonDocument.Parse( await response.Content.ReadAsStringAsync( Token ) );
            JsonElement[] changes = [ .. schedule.RootElement.GetProperty( "items" ).EnumerateArray()
                .Select( item => item.GetProperty( "change" ) )
                .Where( change => change.ValueKind == JsonValueKind.Object ) ];
            JsonElement reschedule = Assert.Single( changes, change => change.GetProperty( "type" ).GetString() == "Reschedule" );
            Assert.NotEqual( JsonValueKind.Null, reschedule.GetProperty( "oldStartsAt" ).ValueKind );
            Assert.Contains( changes, change => change.GetProperty( "type" ).GetString() == "Cancellation" );
            Assert.Contains( changes, change => change.GetProperty( "type" ).GetString() == "RoomChange" );
        }

        private static IQueryable<LessonChange> ChangesOf( AppDbContext db, string group )
        {
            return db.Set<Lesson>().Where( lesson => lesson.Group.Name == group ).SelectMany( lesson => lesson.Changes );
        }

        private async Task<int> CountAsync( Func<AppDbContext, Task<int>> count )
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            return await count( scope.ServiceProvider.GetRequiredService<AppDbContext>() );
        }
    }
}
