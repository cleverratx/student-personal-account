using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dekanat.IntegrationTests
{
    public sealed class HealthTests( WebApplicationFactory<Program> factory ) : IClassFixture<WebApplicationFactory<Program>>
    {
        [Fact]
        public async Task HealthIsAvailableWithoutLogin()
        {
            HttpClient client = factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync( "/api/health", TestContext.Current.CancellationToken );

            Assert.Equal( HttpStatusCode.OK, response.StatusCode );
            Assert.Equal( "application/json", response.Content.Headers.ContentType?.MediaType );

            using JsonDocument body = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync( TestContext.Current.CancellationToken ) );
            Assert.Equal( "Healthy", body.RootElement.GetProperty( "status" ).GetString() );
            Assert.Equal( JsonValueKind.Object, body.RootElement.GetProperty( "checks" ).ValueKind );
        }
    }
}
