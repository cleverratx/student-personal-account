using System.Net;
using System.Text.Json;

namespace Dekanat.IntegrationTests
{
    public sealed class HealthTests( DekanatApiFactory factory )
    {
        [Fact]
        public async Task HealthIsAvailableWithoutLoginAndSeesDatabase()
        {
            HttpClient client = factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync( "/api/health", TestContext.Current.CancellationToken );

            Assert.Equal( HttpStatusCode.OK, response.StatusCode );
            Assert.Equal( "application/json", response.Content.Headers.ContentType?.MediaType );

            using JsonDocument body = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync( TestContext.Current.CancellationToken ) );
            Assert.Equal( "Healthy", body.RootElement.GetProperty( "status" ).GetString() );
            Assert.Equal( "Healthy", body.RootElement.GetProperty( "checks" ).GetProperty( "database" ).GetString() );
        }
    }
}
