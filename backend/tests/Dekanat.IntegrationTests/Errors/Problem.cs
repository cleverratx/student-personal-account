using System.Text.Json;

namespace Dekanat.IntegrationTests.Errors
{
    /// <summary>
    /// Разбор ответа ProblemDetails в тестах.
    /// </summary>
    public sealed class Problem( JsonDocument document, string? requestIdHeader, string? contentType ) : IDisposable
    {
        public JsonElement Body => document.RootElement;

        public string RawBody => document.RootElement.GetRawText();

        public string? RequestIdHeader => requestIdHeader;

        public string? ContentType => contentType;

        public string? Title => Body.GetProperty( "title" ).GetString();

        public int Status => Body.GetProperty( "status" ).GetInt32();

        public string? RequestId => Body.GetProperty( "requestId" ).GetString();

        public static async Task<Problem> ReadAsync( HttpResponseMessage response )
        {
            string body = await response.Content.ReadAsStringAsync( TestContext.Current.CancellationToken );
            response.Headers.TryGetValues( "X-Request-Id", out IEnumerable<string>? requestIds );
            return new Problem(
                JsonDocument.Parse( body ),
                requestIds?.SingleOrDefault(),
                response.Content.Headers.ContentType?.MediaType );
        }

        public void Dispose()
        {
            document.Dispose();
        }
    }
}
