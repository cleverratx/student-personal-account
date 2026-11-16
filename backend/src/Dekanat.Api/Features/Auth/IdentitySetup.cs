using Dekanat.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace Dekanat.Api.Features.Auth
{
    public static class IdentitySetup
    {
        public const int MaxFailedAttempts = 5;
        public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes( 5 );

        /// <summary>
        /// Учётные записи на ASP.NET Core Identity. Пароль хранится только хешем (PasswordHasher, PBKDF2),
        /// после пяти неудачных попыток вход блокируется на пять минут.
        /// </summary>
        public static IServiceCollection AddDekanatIdentity( this IServiceCollection services )
        {
            services.AddIdentityCore<AppUser>( options =>
                {
                    options.User.RequireUniqueEmail = false;

                    options.Password.RequiredLength = 10;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequiredUniqueChars = 4;

                    options.Lockout.AllowedForNewUsers = true;
                    options.Lockout.MaxFailedAccessAttempts = MaxFailedAttempts;
                    options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;
                } )
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddErrorDescriber<RussianIdentityErrorDescriber>()
                .AddSignInManager();

            return services;
        }
    }
}
