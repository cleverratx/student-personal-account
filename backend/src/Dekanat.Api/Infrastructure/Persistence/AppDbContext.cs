using Dekanat.Api.Features.Auth;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
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
        : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>( options ), IDataProtectionKeyContext
    {
        /// <summary>
        /// Ключи, которыми подписана cookie сессии. Лежат в базе, чтобы перезапуск API не разлогинил всех.
        /// </summary>
        public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

        protected override void OnModelCreating( ModelBuilder modelBuilder )
        {
            base.OnModelCreating( modelBuilder );
            modelBuilder.ApplyConfigurationsFromAssembly( typeof( AppDbContext ).Assembly );
        }
    }
}
