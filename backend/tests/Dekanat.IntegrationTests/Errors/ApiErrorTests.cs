using System.Net;

namespace Dekanat.IntegrationTests.Errors
{
    /// <summary>
    /// Те же правила, но на настоящем API из Program.cs.
    /// </summary>
    public sealed class ApiErrorTests( DekanatApiFactory factory )
    {
        [Fact]
        public async Task UnknownApiRouteReturnsRussianProblemDetails()
        {
            HttpClient client = factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync( "/api/no-such-route", TestContext.Current.CancellationToken );

            Assert.Equal( HttpStatusCode.NotFound, response.StatusCode );
            using Problem problem = await Problem.ReadAsync( response );
            Assert.Equal( "application/problem+json", problem.ContentType );
            Assert.Equal( "Не найдено", problem.Title );
            Assert.Equal( problem.RequestIdHeader, problem.RequestId );
        }

        [Fact]
        public async Task WrongMethodReturnsRussianProblemDetails()
        {
            HttpClient client = factory.CreateClient();

            HttpResponseMessage response = await client.DeleteAsync( "/api/health", TestContext.Current.CancellationToken );

            Assert.Equal( HttpStatusCode.MethodNotAllowed, response.StatusCode );
            using Problem problem = await Problem.ReadAsync( response );
            Assert.Equal( "Такой метод здесь не поддерживается", problem.Title );
        }

        [Fact]
        public async Task EverySuccessfulResponseHasItsOwnRequestId()
        {
            HttpClient client = factory.CreateClient();

            HttpResponseMessage first = await client.GetAsync( "/api/health", TestContext.Current.CancellationToken );
            HttpResponseMessage second = await client.GetAsync( "/api/health", TestContext.Current.CancellationToken );

            string firstId = Assert.Single( first.Headers.GetValues( "X-Request-Id" ) );
            string secondId = Assert.Single( second.Headers.GetValues( "X-Request-Id" ) );
            Assert.False( string.IsNullOrWhiteSpace( firstId ) );
            Assert.NotEqual( firstId, secondId );
        }
    }
}
