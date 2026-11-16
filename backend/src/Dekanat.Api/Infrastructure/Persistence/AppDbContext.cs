using Dekanat.Api.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dekanat.Api.Infrastructure.Persistence
{
    /// <summary>
    /// Единый контекст базы. Сущности разделов лежат в Features/&lt;Раздел&gt;, а их настройки
    /// (IEntityTypeConfiguration) подхватываются отсюда автоматически. Учётные записи и роли хранит Identity.
    /// </summary>
    public sealed class AppDbContext( DbContextOptions<AppDbContext> options )
        : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>( options )
    {
        protected override void OnModelCreating( ModelBuilder modelBuilder )
        {
            base.OnModelCreating( modelBuilder );
            modelBuilder.ApplyConfigurationsFromAssembly( typeof( AppDbContext ).Assembly );
        }
    }
}
