using Microsoft.AspNetCore.Identity;

namespace Dekanat.Api.Features.Auth
{
    /// <summary>
    /// Тексты ошибок Identity по-русски: их видят сотрудники при заведении учётных записей и мы в тестах.
    /// </summary>
    public sealed class RussianIdentityErrorDescriber : IdentityErrorDescriber
    {
        public override IdentityError PasswordTooShort( int length )
        {
            return Error( nameof( PasswordTooShort ), $"Пароль должен быть не короче {length} символов." );
        }

        public override IdentityError PasswordRequiresDigit()
        {
            return Error( nameof( PasswordRequiresDigit ), "В пароле должна быть хотя бы одна цифра." );
        }

        public override IdentityError PasswordRequiresLower()
        {
            return Error( nameof( PasswordRequiresLower ), "В пароле должна быть хотя бы одна строчная буква." );
        }

        public override IdentityError PasswordRequiresUpper()
        {
            return Error( nameof( PasswordRequiresUpper ), "В пароле должна быть хотя бы одна заглавная буква." );
        }

        public override IdentityError PasswordRequiresNonAlphanumeric()
        {
            return Error( nameof( PasswordRequiresNonAlphanumeric ), "В пароле должен быть хотя бы один знак, не буква и не цифра." );
        }

        public override IdentityError PasswordRequiresUniqueChars( int uniqueChars )
        {
            return Error( nameof( PasswordRequiresUniqueChars ), $"В пароле должно быть хотя бы {uniqueChars} разных символа." );
        }

        public override IdentityError DuplicateUserName( string userName )
        {
            return Error( nameof( DuplicateUserName ), "Этот логин уже занят." );
        }

        public override IdentityError InvalidUserName( string? userName )
        {
            return Error( nameof( InvalidUserName ), "В логине можно использовать только латиницу, цифры и знаки . - _ @ +" );
        }

        public override IdentityError UserLockoutNotEnabled()
        {
            return Error( nameof( UserLockoutNotEnabled ), "Для этой учётной записи блокировка выключена." );
        }

        public override IdentityError PasswordMismatch()
        {
            return Error( nameof( PasswordMismatch ), "Неверный пароль." );
        }

        public override IdentityError DefaultError()
        {
            return Error( nameof( DefaultError ), "Не удалось выполнить действие с учётной записью." );
        }

        private static IdentityError Error( string code, string description )
        {
            return new IdentityError { Code = code, Description = description };
        }
    }
}
