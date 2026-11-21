using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dekanat.Api.Features.Staff
{
    /// <summary>Аудитория объявления: все студенты факультета, конкретная группа или весь вуз.</summary>
    public enum AnnouncementAudienceType
    {
        All,
        Faculty,
        Group,
    }

    /// <summary>
    /// Объявление деканата. Аудитория определяется комбинацией полей:
    /// All → FacultyId и GroupId null; Faculty → FacultyId заполнен, GroupId null; Group → оба заполнены.
    /// Время в UTC.
    /// </summary>
    public sealed class Announcement
    {
        public Guid Id { get; set; }

        public required string Title { get; set; }

        public required string Content { get; set; }

        public bool IsImportant { get; set; }

        public DateTime PublishedAt { get; set; }

        public DateTime ExpiresAt { get; set; }

        /// <summary>Кто создал объявление. Пусто, если профиль сотрудника удалили.</summary>
        public Guid? AuthorStaffProfileId { get; set; }

        public AnnouncementAudienceType AudienceType { get; set; }

        /// <summary>Факультет для рассылки. Null означает «весь вуз».</summary>
        public Guid? FacultyId { get; set; }

        public Faculty? Faculty { get; set; }

        /// <summary>Группа для рассылки. Null означает «весь факультет» или «весь вуз».</summary>
        public Guid? GroupId { get; set; }

        public StudyGroup? Group { get; set; }
    }

    public sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
    {
        public void Configure( EntityTypeBuilder<Announcement> builder )
        {
            builder.ToTable( "announcements", table =>
                table.HasCheckConstraint(
                    "ck_announcements_audience_type",
                    "audience_type IN ('All', 'Faculty', 'Group')" ) );

            builder.Property( a => a.Title ).HasMaxLength( 200 );
            builder.Property( a => a.Content ).HasMaxLength( 5000 );
            builder.Property( a => a.AudienceType ).HasConversion<string>().HasMaxLength( 16 );

            // Объявления для студента: актуальные по факультету или группе.
            builder.HasIndex( a => new { a.FacultyId, a.ExpiresAt } );
            builder.HasIndex( a => new { a.GroupId, a.ExpiresAt } );

            builder.HasOne( a => a.Faculty ).WithMany()
                .HasForeignKey( a => a.FacultyId )
                .HasConstraintName( "fk_announcements_faculties_faculty_id" )
                .OnDelete( DeleteBehavior.SetNull );

            builder.HasOne( a => a.Group ).WithMany()
                .HasForeignKey( a => a.GroupId )
                .HasConstraintName( "fk_announcements_groups_group_id" )
                .OnDelete( DeleteBehavior.SetNull );

            builder.HasOne<StaffProfile>().WithMany()
                .HasForeignKey( a => a.AuthorStaffProfileId )
                .HasConstraintName( "fk_announcements_staff_profiles_author_staff_profile_id" )
                .OnDelete( DeleteBehavior.SetNull );
        }
    }
}
