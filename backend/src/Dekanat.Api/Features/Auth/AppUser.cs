using Microsoft.AspNetCore.Identity;

namespace Dekanat.Api.Features.Auth
{
    /// <summary>
    /// Учётная запись: логин, хеш пароля, счётчик неудачных попыток и блокировка. Всё это хранит Identity.
    /// ФИО и учебные данные лежат в профилях студента и сотрудника, а не здесь.
    /// </summary>
    public sealed class AppUser : IdentityUser<Guid>
    {
    }
}
