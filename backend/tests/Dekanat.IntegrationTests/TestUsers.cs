using Dekanat.Api.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Dekanat.IntegrationTests
{
    /// <summary>
    /// Учётная запись для теста. Логин уникальный, чтобы тесты не мешали друг другу на общей базе.
    /// </summary>
    public sealed record TestUser( Guid Id, string Login, string Password, string Role );

    public static class TestUsers
    {
        public static async Task<TestUser> CreateAsync( IServiceProvider services, string role = Roles.Student )
        {
            await using AsyncServiceScope scope = services.CreateAsyncScope();
            UserManager<AppUser> users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

            string login = $"test.{Guid.NewGuid():N}"[ ..20 ];
            string password = $"Pa55word{Guid.NewGuid():N}"[ ..20 ];
            AppUser user = new() { Id = Guid.NewGuid(), UserName = login };

            IdentityResult created = await users.CreateAsync( user, password );
            Assert.True( created.Succeeded, string.Join( "; ", created.Errors.Select( error => error.Description ) ) );
            IdentityResult inRole = await users.AddToRoleAsync( user, role );
            Assert.True( inRole.Succeeded, string.Join( "; ", inRole.Errors.Select( error => error.Description ) ) );

            return new TestUser( user.Id, login, password, role );
        }
    }
}
