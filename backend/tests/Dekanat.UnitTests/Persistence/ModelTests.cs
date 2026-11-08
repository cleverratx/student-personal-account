using Dekanat.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Dekanat.UnitTests.Persistence
{
    public sealed class ModelTests
    {
        [Fact]
        public void ModelMatchesLatestMigration()
        {
            using AppDbContext db = new DesignTimeDbContextFactory().CreateDbContext( [] );

            // Если тест упал, значит, сущности поменяли, а миграцию не создали:
            // dotnet ef migrations add <Имя> --project src/Dekanat.Api --output-dir Infrastructure/Persistence/Migrations
            Assert.False( db.Database.HasPendingModelChanges() );
        }

        [Fact]
        public void TablesAndColumnsUseSnakeCase()
        {
            DbContextOptionsBuilder<NamingContext> options = new();
            DatabaseSetup.Configure( options, "Host=localhost;Database=dekanat" );
            using NamingContext db = new( options.Options );

            IEntityType entity = db.Model.FindEntityType( typeof( LessonChangeSample ) )!;
            StoreObjectIdentifier table = StoreObjectIdentifier.Table( entity.GetTableName()! );

            Assert.Equal( "lesson_change_samples", entity.GetTableName() );
            Assert.Equal( "old_starts_at", entity.FindProperty( nameof( LessonChangeSample.OldStartsAt ) )!.GetColumnName( table ) );
            Assert.Equal( "changed_by_user_id", entity.FindProperty( nameof( LessonChangeSample.ChangedByUserId ) )!.GetColumnName( table ) );
        }

        private sealed class NamingContext( DbContextOptions<NamingContext> options ) : DbContext( options )
        {
            public DbSet<LessonChangeSample> LessonChangeSamples => Set<LessonChangeSample>();
        }

        private sealed class LessonChangeSample
        {
            public Guid Id { get; set; }

            public DateTime OldStartsAt { get; set; }

            public Guid ChangedByUserId { get; set; }
        }
    }
}
