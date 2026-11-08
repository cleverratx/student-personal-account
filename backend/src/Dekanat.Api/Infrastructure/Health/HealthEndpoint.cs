using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dekanat.Api.Infrastructure.Health
{
    /// <summary>
    /// GET /api/health: жив ли API и доступны ли его зависимости. Нужен для проверок в Docker и внешнего мониторинга.
    /// </summary>
    public static class HealthEndpoint
    {
        public static RouteHandlerBuilder MapHealth( this IEndpointRouteBuilder routes )
        {
            return routes.MapGet( "/health", GetHealthAsync )
                .WithName( "GetHealth" )
                .WithTags( "Health" )
                .WithSummary( "Состояние API и его зависимостей" )
                .AllowAnonymous()
                .Produces<HealthResponse>( StatusCodes.Status200OK )
                .Produces<HealthResponse>( StatusCodes.Status503ServiceUnavailable );
        }

        /// <summary>
        /// Healthy и Degraded отвечают 200, Unhealthy отвечает 503, чтобы мониторинг понимал ответ по коду.
        /// </summary>
        public static int ToStatusCode( HealthStatus status )
        {
            return status == HealthStatus.Unhealthy
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status200OK;
        }

        private static async Task<IResult> GetHealthAsync( HealthCheckService healthChecks, CancellationToken cancellationToken )
        {
            HealthReport report = await healthChecks.CheckHealthAsync( cancellationToken );

            // Наружу отдаём только статусы проверок, без описаний и исключений.
            HealthResponse response = new(
                report.Status,
                report.Entries.ToDictionary( entry => entry.Key, entry => entry.Value.Status ) );

            return TypedResults.Json( response, statusCode: ToStatusCode( report.Status ) );
        }
    }

    /// <param name="Status">Общее состояние: Healthy, Degraded или Unhealthy.</param>
    /// <param name="Checks">Состояние каждой проверки по имени, например database.</param>
    public sealed record HealthResponse( HealthStatus Status, IReadOnlyDictionary<string, HealthStatus> Checks );
}
