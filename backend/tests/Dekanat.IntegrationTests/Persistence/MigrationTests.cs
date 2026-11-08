using Dekanat.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dekanat.IntegrationTests.Persistence
{
    public sealed class MigrationTests( DekanatApiFactory factory )
    {
        [Fact]
        public async Task AllMigrationsAreAppliedOnStartup()
        {
            // Первое обращение к фабрике запускает API, а вместе с ним и миграции.
            _ = factory.CreateClient();

            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;

            IEnumerable<string> applied = await db.Database.GetAppliedMigrationsAsync( cancellationToken );
            IEnumerable<string> pending = await db.Database.GetPendingMigrationsAsync( cancellationToken );

            Assert.Equal( db.Database.GetMigrations(), applied );
            Assert.Empty( pending );
        }
    }
}
