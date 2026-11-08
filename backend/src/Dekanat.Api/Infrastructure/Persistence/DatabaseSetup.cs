using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dekanat.Api.Infrastructure.Persistence
{
    public static class DatabaseSetup
    {
        /// <summary>
        /// Ключ ConnectionStrings:Default. В Docker приходит из переменной окружения ConnectionStrings__Default,
        /// локально из user-secrets.
        /// </summary>
        public const string ConnectionStringName = "Default";

        public static IServiceCollection AddDatabase( this IServiceCollection services )
        {
            // Строку подключения читаем при создании контекста, а не при старте: так её можно подменить
            // в тестах, а сборка OpenAPI-документа обходится без неё.
            services.AddDbContext<AppDbContext>( ( provider, options ) =>
            {
                string connectionString = provider.GetRequiredService<IConfiguration>()
                    .GetConnectionString( ConnectionStringName )
                    ?? throw new InvalidOperationException(
                        $"Не задана строка подключения ConnectionStrings:{ConnectionStringName}. "
                        + "В Docker её собирает compose.yaml из .env, для dotnet run задайте её через dotnet user-secrets." );

                Configure( options, connectionString );
            } );

            services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>( "database", timeout: TimeSpan.FromSeconds( 5 ) );

            return services;
        }

        /// <summary>
        /// Общие настройки провайдера: и для приложения, и для dotnet ef, и для тестов.
        /// </summary>
        public static DbContextOptionsBuilder Configure( DbContextOptionsBuilder options, string connectionString )
        {
            // Kerberos не используем. Без этого Npgsql пытается загрузить libgssapi, которой нет в образе aspnet,
            // и пишет в лог ошибку при каждом новом соединении.
            NpgsqlConnectionStringBuilder connection = new( connectionString )
            {
                GssEncryptionMode = GssEncryptionMode.Disable,
            };

            return options
                .UseNpgsql( connection.ConnectionString, npgsql => npgsql.EnableRetryOnFailure() )
                .UseSnakeCaseNamingConvention();
        }

        /// <summary>
        /// Применяет миграции, которых ещё нет в базе. Вызывается при старте API, до приёма запросов.
        /// </summary>
        public static async Task MigrateDatabaseAsync( this WebApplication app )
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }
    }
}
