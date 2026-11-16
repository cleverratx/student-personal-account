using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace Dekanat.Api.Features.Auth
{
    public enum UserRole
    {
        Student,
        Staff,
    }

    public sealed class LoginRequest
    {
        [Required( ErrorMessage = "Введите логин" )]
        [MaxLength( 64, ErrorMessage = "Логин не длиннее 64 символов" )]
        public string? Login { get; init; }

        [Required( ErrorMessage = "Введите пароль" )]
        [MaxLength( 128, ErrorMessage = "Пароль не длиннее 128 символов" )]
        public string? Password { get; init; }
    }

    /// <param name="Role">Роль вошедшего. По ней фронт выбирает стартовый экран.</param>
    public sealed record LoginResponse( UserRole Role );

    /// <param name="Id">Id учётной записи.</param>
    /// <param name="Login">Логин, под которым вошли.</param>
    /// <param name="Role">Роль: студент или сотрудник деканата.</param>
    public sealed record MeResponse( Guid Id, string Login, UserRole Role );

    public static class AuthEndpoints
    {
        public static RouteGroupBuilder MapAuth( this RouteGroupBuilder api )
        {
            RouteGroupBuilder auth = api.MapGroup( "/auth" )
                .WithTags( "Auth" )
                .RequireRateLimiting( SessionSetup.AuthRateLimitPolicy )
                .ProducesProblem( StatusCodes.Status429TooManyRequests );

            auth.MapPost( "/login", LoginAsync )
                .AllowAnonymous()
                .WithName( "Login" )
                .WithSummary( "Вход по логину и паролю, ставит cookie сессии" )
                .Produces<LoginResponse>()
                .ProducesValidationProblem()
                .ProducesProblem( StatusCodes.Status401Unauthorized );

            auth.MapPost( "/logout", LogoutAsync )
                .WithName( "Logout" )
                .WithSummary( "Выход, удаляет cookie сессии" )
                .Produces( StatusCodes.Status204NoContent )
                .ProducesProblem( StatusCodes.Status401Unauthorized );

            api.MapGet( "/me", GetMe )
                .WithTags( "Auth" )
                .WithName( "GetMe" )
                .WithSummary( "Кто сейчас вошёл" )
                .Produces<MeResponse>()
                .ProducesProblem( StatusCodes.Status401Unauthorized );

            return api;
        }

        private static async Task<IResult> LoginAsync(
            LoginRequest request,
            UserManager<AppUser> users,
            SignInManager<AppUser> signIn,
            UnknownLoginAttempts unknownLogins,
            ILoggerFactory loggers )
        {
            ILogger logger = loggers.CreateLogger( "Dekanat.Api.Features.Auth" );
            string login = request.Login!.Trim();
            AppUser? user = await users.FindByNameAsync( login );

            if ( user is null )
            {
                string normalized = users.NormalizeName( login );
                // Тратим столько же времени, сколько на проверку настоящего пароля, чтобы по скорости ответа
                // нельзя было понять, что такого логина нет.
                users.PasswordHasher.HashPassword( new AppUser(), request.Password! );

                TimeSpan? locked = unknownLogins.LockedFor( normalized ) ?? unknownLogins.RegisterFailure( normalized );
                return locked is null ? AuthProblems.InvalidCredentials() : AuthProblems.TooManyAttempts( locked.Value );
            }

            SignInResult result = await signIn.PasswordSignInAsync( user, request.Password!, isPersistent: true, lockoutOnFailure: true );

            if ( result.IsLockedOut )
            {
                logger.LogWarning( "Учётная запись {UserId} заблокирована после неудачных попыток входа", user.Id );
                DateTimeOffset? lockoutEnd = await users.GetLockoutEndDateAsync( user );
                TimeSpan left = ( lockoutEnd ?? DateTimeOffset.UtcNow ) - DateTimeOffset.UtcNow;
                return AuthProblems.TooManyAttempts( left > TimeSpan.Zero ? left : IdentitySetup.LockoutDuration );
            }

            if ( !result.Succeeded )
            {
                logger.LogInformation( "Неудачная попытка входа в учётную запись {UserId}", user.Id );
                return AuthProblems.InvalidCredentials();
            }

            UserRole? role = RoleOf( await users.GetRolesAsync( user ) );
            if ( role is null )
            {
                // Учётная запись без роли кабинета войти не может.
                await signIn.SignOutAsync();
                logger.LogWarning( "Учётная запись {UserId} без роли пыталась войти", user.Id );
                return AuthProblems.InvalidCredentials();
            }

            return TypedResults.Ok( new LoginResponse( role.Value ) );
        }

        private static async Task<IResult> LogoutAsync( SignInManager<AppUser> signIn )
        {
            await signIn.SignOutAsync();
            return TypedResults.NoContent();
        }

        private static IResult GetMe( ClaimsPrincipal principal )
        {
            UserRole? role = RoleOf( principal.FindAll( ClaimTypes.Role ).Select( claim => claim.Value ).ToList() );
            string? id = principal.FindFirstValue( ClaimTypes.NameIdentifier );
            string? login = principal.Identity?.Name;

            if ( role is null || id is null || login is null )
            {
                return TypedResults.Unauthorized();
            }

            return TypedResults.Ok( new MeResponse( Guid.Parse( id ), login, role.Value ) );
        }

        private static UserRole? RoleOf( IList<string> roles )
        {
            if ( roles.Contains( Roles.Staff ) )
            {
                return UserRole.Staff;
            }

            return roles.Contains( Roles.Student ) ? UserRole.Student : null;
        }
    }
}
