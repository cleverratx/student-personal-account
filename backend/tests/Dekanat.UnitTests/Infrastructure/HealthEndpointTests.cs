using Dekanat.Api.Infrastructure.Health;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dekanat.UnitTests.Infrastructure
{
    public sealed class HealthEndpointTests
    {
        [Theory]
        [InlineData( HealthStatus.Healthy, StatusCodes.Status200OK )]
        [InlineData( HealthStatus.Degraded, StatusCodes.Status200OK )]
        [InlineData( HealthStatus.Unhealthy, StatusCodes.Status503ServiceUnavailable )]
        public void StatusCodeFollowsHealthStatus( HealthStatus status, int expectedStatusCode )
        {
            Assert.Equal( expectedStatusCode, HealthEndpoint.ToStatusCode( status ) );
        }
    }
}
