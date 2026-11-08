using Dekanat.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Testcontainers.PostgreSql;

namespace Dekanat.IntegrationTests.Persistence
{
    public sealed class DatabaseHealthCheckTests( DekanatApiFactory factory )
    {
        [Fact]
        public async Task ReportsHealthyWhenDatabaseIsReachable()
        {
            HealthCheckResult result = await CheckAsync( factory.ConnectionString );

            Assert.Equal( HealthStatus.Healthy, result.Status );
        }

        [Fact]
        public async Task ReportsUnhealthyWhenDatabaseIsUnreachable()
        {
            // На порту 1 никто не слушает, соединение сразу отклоняется.
            HealthCheckResult result = await CheckAsync( "Host=127.0.0.1;Port=1;Database=dekanat;Username=dekanat;Timeout=3" );

            Assert.Equal( HealthStatus.Unhealthy, result.Status );
        }

        [Fact]
        public async Task ReportsUnhealthyAfterDatabaseStops()
        {
            // Отдельный контейнер: общий останавливать нельзя, на нём работают другие тесты.
            await using PostgreSqlContainer postgres = new PostgreSqlBuilder( DekanatApiFactory.PostgresImage ).Build();
            await postgres.StartAsync( TestContext.Current.CancellationToken );
            string connectionString = postgres.GetConnectionString();

            // Первая проверка оставляет в пуле открытое соединение. Раньше после остановки базы
            // проверка брала его из пула и отвечала Healthy.
            Assert.Equal( HealthStatus.Healthy, ( await CheckAsync( connectionString ) ).Status );

            await postgres.StopAsync( TestContext.Current.CancellationToken );

            Assert.Equal( HealthStatus.Unhealthy, ( await CheckAsync( connectionString ) ).Status );
        }

        private static async Task<HealthCheckResult> CheckAsync( string connectionString )
        {
            DbContextOptionsBuilder<AppDbContext> options = new();
            DatabaseSetup.Configure( options, connectionString );
            await using AppDbContext db = new( options.Options );

            DatabaseHealthCheck check = new( db );
            HealthCheckContext context = new()
            {
                Registration = new HealthCheckRegistration( "database", check, HealthStatus.Unhealthy, tags: null ),
            };

            return await check.CheckHealthAsync( context, TestContext.Current.CancellationToken );
        }
    }
}
