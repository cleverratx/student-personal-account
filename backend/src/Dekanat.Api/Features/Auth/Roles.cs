namespace Dekanat.Api.Features.Auth
{
    /// <summary>
    /// Роли кабинета. Других ролей нет, роль Admin не используем.
    /// </summary>
    public static class Roles
    {
        public const string Student = "Student";
        public const string Staff = "Staff";

        // Постоянные id, чтобы роли создавались миграцией и совпадали на всех базах.
        public static readonly Guid StudentId = new( "6b7f0c1e-2a4d-4c8e-9f1a-3d5e7b9c1a01" );
        public static readonly Guid StaffId = new( "6b7f0c1e-2a4d-4c8e-9f1a-3d5e7b9c1a02" );
    }
}
