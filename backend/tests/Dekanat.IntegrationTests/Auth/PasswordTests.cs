using Dekanat.Api.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dekanat.IntegrationTests.Auth
{
    public sealed class PasswordTests( DekanatApiFactory factory )
    {
        [Fact]
        public async Task DatabaseStoresOnlyPasswordHash()
        {
            TestUser user = await TestUsers.CreateAsync( factory.Services );

            await using NpgsqlConnection connection = new( factory.ConnectionString );
            await connection.OpenAsync( TestContext.Current.CancellationToken );
            await using NpgsqlCommand command = new( "SELECT password_hash FROM users WHERE id = @id", connection );
            command.Parameters.AddWithValue( "id", user.Id );
            string? hash = ( string? )await command.ExecuteScalarAsync( TestContext.Current.CancellationToken );

            Assert.NotNull( hash );
            Assert.DoesNotContain( user.Password, hash, StringComparison.Ordinal );
            // Формат PasswordHasher v3: первый байт 0x01, дальше PBKDF2 с солью.
            Assert.Equal( 0x01, Convert.FromBase64String( hash )[ 0 ] );
        }

        [Fact]
        public async Task WeakPasswordIsRejectedWithRussianReasons()
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            UserManager<AppUser> users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            AppUser user = new() { Id = Guid.NewGuid(), UserName = $"weak.{Guid.NewGuid():N}"[ ..16 ] };

            IdentityResult result = await users.CreateAsync( user, "qwerty" );

            Assert.False( result.Succeeded );
            Assert.Contains( result.Errors, error => error.Description == "Пароль должен быть не короче 10 символов." );
            Assert.Contains( result.Errors, error => error.Description == "В пароле должна быть хотя бы одна цифра." );
            Assert.Contains( result.Errors, error => error.Description == "В пароле должна быть хотя бы одна заглавная буква." );
        }
    }
}
