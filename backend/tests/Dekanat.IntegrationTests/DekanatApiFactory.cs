using Dekanat.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture( typeof( Dekanat.IntegrationTests.DekanatApiFactory ) )]

namespace Dekanat.IntegrationTests
{
    /// <summary>
    /// API в памяти и настоящий PostgreSQL 18 в Docker. Один на все тесты сборки: контейнер стартует
    /// несколько секунд, поэтому данные в тестах делаем уникальными, а не чистим базу между тестами.
    /// </summary>
    public sealed class DekanatApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        public const string PostgresImage = "postgres:18-alpine";

        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder( PostgresImage ).Build();

        public string ConnectionString => _postgres.GetConnectionString();

        public async ValueTask InitializeAsync()
        {
            await _postgres.StartAsync();
        }

        protected override void ConfigureWebHost( IWebHostBuilder builder )
        {
            builder.UseSetting( $"ConnectionStrings:{DatabaseSetup.ConnectionStringName}", ConnectionString );
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _postgres.DisposeAsync();
        }
    }
}
