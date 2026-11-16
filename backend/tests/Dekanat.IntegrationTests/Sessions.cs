using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dekanat.IntegrationTests
{
    /// <summary>
    /// Клиенты с cookie. Адрес https: cookie сессии с флагом Secure по http клиент не отправил бы.
    /// </summary>
    public static class Sessions
    {
        public static HttpClient CreateBrowser<TEntryPoint>( this WebApplicationFactory<TEntryPoint> factory )
            where TEntryPoint : class
        {
            return factory.CreateClient( new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri( "https://localhost" ),
                HandleCookies = true,
                AllowAutoRedirect = false,
            } );
        }

        public static async Task<HttpResponseMessage> LoginAsync( this HttpClient client, string login, string password )
        {
            return await client.PostAsJsonAsync(
                "/api/auth/login",
                new { login, password },
                TestContext.Current.CancellationToken );
        }

        /// <summary>
        /// Новый пользователь с ролью и браузер, в котором он уже вошёл.
        /// </summary>
        public static async Task<(TestUser User, HttpClient Client)> SignedInAsync(
            this DekanatApiFactory factory,
            string role = Dekanat.Api.Features.Auth.Roles.Student )
        {
            TestUser user = await TestUsers.CreateAsync( factory.Services, role );
            HttpClient client = factory.CreateBrowser();
            HttpResponseMessage response = await client.LoginAsync( user.Login, user.Password );
            Assert.Equal( HttpStatusCode.OK, response.StatusCode );
            return (user, client);
        }
    }
}
