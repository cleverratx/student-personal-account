namespace Dekanat.Api.Features.Auth
{
    /// <summary>
    /// Названия политик авторизации. Эндпоинты студента требуют <see cref="Student"/>, всё под /api/staff требует
    /// <see cref="Staff"/>. Роль проверяется только здесь, в эндпоинтах роли строкой не пишем.
    /// </summary>
    public static class AuthorizationPolicies
    {
        public const string Student = "Student";
        public const string Staff = "Staff";
    }
}
