using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dekanat.Api.Features.Staff
{
    /// <summary>Кому адресовано объявление.</summary>
    public enum AudienceType
    {
        /// <summary>Всем студентам вуза.</summary>
        All,

        /// <summary>Студентам факультета сотрудника.</summary>
        Faculty,

        /// <summary>Конкретной учебной группе.</summary>
        Group,
    }

    /// <summary>
    /// Объявление деканата. Аудитория описывается полями <see cref="AudienceType"/>, <see cref="FacultyId"/>
    /// и <see cref="GroupId"/>: только одно из них актуально в зависимости от типа аудитории.
    /// </summary>
    public sealed class Announcement
    {
        public Guid Id { get; set; }

        public required string Title { get; set; }

        public required string Content { get; set; }

        public bool IsImportant { get; set; }

        public DateTime PublishedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public AudienceType AudienceType { get; set; }

        /// <summary>Факультет-аудитория. Не null, когда <see cref="AudienceType"/> равен <see cref="AudienceType.Faculty"/>.</summary>
        public Guid? FacultyId { get; set; }

        /// <summary>Группа-аудитория. Не null, когда <see cref="AudienceType"/> равен <see cref="AudienceType.Group"/>.</summary>
        public Guid? GroupId { get; set; }

        /// <summary>Кто создал. Null, если учётную запись удалили.</summary>
        public Guid? AuthorStaffProfileId { get; set; }
    }

    public sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
    {
        public void Configure( EntityTypeBuilder<Announcement> builder )
        {
            builder.ToTable( "announcements" );
            builder.Property( a => a.Title ).HasMaxLength( 200 );
            builder.Property( a => a.Content ).HasMaxLength( 2000 );
            builder.Property( a => a.AudienceType )
                .HasConversion<string>()
                .HasMaxLength( 16 );

            // Быстрый поиск объявлений по аудитории с фильтром на срок актуальности.
            builder.HasIndex( a => new { a.FacultyId, a.ExpiresAt } )
                .HasDatabaseName( "ix_announcements_faculty_id_expires_at" );
            builder.HasIndex( a => new { a.GroupId, a.ExpiresAt } )
                .HasDatabaseName( "ix_announcements_group_id_expires_at" );

            builder.HasOne( "Dekanat.Api.Features.Profile.Faculty", null )
                .WithMany()
                .HasForeignKey( nameof( Announcement.FacultyId ) )
                .OnDelete( DeleteBehavior.SetNull )
                .HasConstraintName( "fk_announcements_faculties_faculty_id" );

            builder.HasOne( "Dekanat.Api.Features.Profile.StudyGroup", null )
                .WithMany()
                .HasForeignKey( nameof( Announcement.GroupId ) )
                .OnDelete( DeleteBehavior.SetNull )
                .HasConstraintName( "fk_announcements_groups_group_id" );

            builder.HasOne( "Dekanat.Api.Features.Staff.StaffProfile", null )
                .WithMany()
                .HasForeignKey( nameof( Announcement.AuthorStaffProfileId ) )
                .OnDelete( DeleteBehavior.SetNull )
                .HasConstraintName( "fk_announcements_staff_profiles_author_staff_profile_id" );
        }
    }
}
