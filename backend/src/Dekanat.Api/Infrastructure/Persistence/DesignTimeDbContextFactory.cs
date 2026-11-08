using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dekanat.Api.Infrastructure.Persistence
{
    /// <summary>
    /// Контекст для dotnet ef. Чтобы создать миграцию, база не нужна, хватает настроек провайдера.
    /// Для dotnet ef database update передайте настоящую строку через --connection.
    /// </summary>
    public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext( string[] args )
        {
            DbContextOptionsBuilder<AppDbContext> options = new();
            DatabaseSetup.Configure( options, "Host=localhost;Database=dekanat" );
            return new AppDbContext( options.Options );
        }
    }
}
