using Microsoft.EntityFrameworkCore;

namespace Dekanat.Api.Infrastructure.Persistence
{
    /// <summary>
    /// Единый контекст базы. Сущности разделов лежат в Features/&lt;Раздел&gt;, а их настройки
    /// (IEntityTypeConfiguration) подхватываются отсюда автоматически.
    /// </summary>
    public sealed class AppDbContext( DbContextOptions<AppDbContext> options ) : DbContext( options )
    {
        protected override void OnModelCreating( ModelBuilder modelBuilder )
        {
            modelBuilder.ApplyConfigurationsFromAssembly( typeof( AppDbContext ).Assembly );
        }
    }
}
