using Dekanat.Api.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Dekanat.IntegrationTests.Auth
{
    public sealed class LockoutTests( DekanatApiFactory factory )
    {
        [Fact]
        public async Task FiveWrongPasswordsLockAccountForFiveMinutes()
        {
            TestUser testUser = await TestUsers.CreateAsync( factory.Services );
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            SignInManager<AppUser> signIn = scope.ServiceProvider.GetRequiredService<SignInManager<AppUser>>();
            UserManager<AppUser> users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            AppUser user = ( await users.FindByIdAsync( testUser.Id.ToString() ) )!;

            for ( int attempt = 1; attempt < IdentitySetup.MaxFailedAttempts; attempt++ )
            {
                SignInResult wrong = await signIn.CheckPasswordSignInAsync( user, "Wrong-password-1", lockoutOnFailure: true );
                Assert.False( wrong.IsLockedOut, $"Заблокировало уже после {attempt} попыток" );
            }

            SignInResult fifth = await signIn.CheckPasswordSignInAsync( user, "Wrong-password-1", lockoutOnFailure: true );
            Assert.True( fifth.IsLockedOut );

            // Пока блокировка не кончилась, не пускает даже с верным паролем.
            SignInResult correct = await signIn.CheckPasswordSignInAsync( user, testUser.Password, lockoutOnFailure: true );
            Assert.True( correct.IsLockedOut );

            DateTimeOffset? lockoutEnd = await users.GetLockoutEndDateAsync( user );
            Assert.NotNull( lockoutEnd );
            TimeSpan left = lockoutEnd.Value - DateTimeOffset.UtcNow;
            Assert.InRange( left, TimeSpan.FromMinutes( 4 ), TimeSpan.FromMinutes( 5 ) );
        }

        [Fact]
        public async Task SuccessfulSignInResetsFailedAttempts()
        {
            TestUser testUser = await TestUsers.CreateAsync( factory.Services );
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            SignInManager<AppUser> signIn = scope.ServiceProvider.GetRequiredService<SignInManager<AppUser>>();
            UserManager<AppUser> users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            AppUser user = ( await users.FindByIdAsync( testUser.Id.ToString() ) )!;

            for ( int attempt = 1; attempt < IdentitySetup.MaxFailedAttempts; attempt++ )
            {
                await signIn.CheckPasswordSignInAsync( user, "Wrong-password-1", lockoutOnFailure: true );
            }

            SignInResult correct = await signIn.CheckPasswordSignInAsync( user, testUser.Password, lockoutOnFailure: true );

            Assert.True( correct.Succeeded );
            Assert.Equal( 0, await users.GetAccessFailedCountAsync( user ) );
        }
    }
}
