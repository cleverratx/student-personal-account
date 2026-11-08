using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dekanat.Api.Infrastructure.Persistence
{
    /// <summary>
    /// Проверка "database" в GET /api/health: отвечает ли PostgreSQL на запрос.
    /// </summary>
    public sealed class DatabaseHealthCheck( AppDbContext db ) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync( HealthCheckContext context, CancellationToken cancellationToken = default )
        {
            // Работаем с соединением самого Npgsql, а не через CanConnectAsync: тот идёт через стратегию повторов
            // EnableRetryOnFailure, и при упавшей базе ответ health растягивается на минуты.
            // Одного открытия мало: пул отдаёт старое соединение, даже если база уже остановлена, поэтому шлём SELECT 1.
            DbConnection connection = db.Database.GetDbConnection();
            try
            {
                await connection.OpenAsync( cancellationToken );
                await using DbCommand command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync( cancellationToken );
                return HealthCheckResult.Healthy();
            }
            catch ( DbException exception )
            {
                return new HealthCheckResult( context.Registration.FailureStatus, "База данных недоступна", exception );
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
    }
}
