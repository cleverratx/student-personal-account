using Dekanat.Api.Features.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dekanat.Api.Features.Profile
{
    /// <summary>
    /// Персональные данные студента. Видит их только сам студент, позже сотрудник своего факультета.
    /// </summary>
    public sealed class StudentProfile
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public required string FullName { get; set; }

        public Guid GroupId { get; set; }

        public StudyGroup Group { get; set; } = null!;

        public required string GradeBookNumber { get; set; }
    }

    /// <summary>
    /// Сотрудник деканата. Студенты его профиль не видят.
    /// </summary>
    public sealed class StaffProfile
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public required string FullName { get; set; }

        public Guid FacultyId { get; set; }

        public Faculty Faculty { get; set; } = null!;

        public required string Position { get; set; }

        public string? Office { get; set; }
    }

    public sealed class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
    {
        public void Configure( EntityTypeBuilder<StudentProfile> builder )
        {
            builder.ToTable( "student_profiles" );
            builder.Property( profile => profile.FullName ).HasMaxLength( 200 );
            builder.Property( profile => profile.GradeBookNumber ).HasMaxLength( 32 );
            builder.HasIndex( profile => profile.UserId ).IsUnique();
            builder.HasIndex( profile => profile.GradeBookNumber ).IsUnique();
            builder.HasOne<AppUser>().WithOne().HasForeignKey<StudentProfile>( profile => profile.UserId ).OnDelete( DeleteBehavior.Cascade );
            builder.HasOne( profile => profile.Group ).WithMany().HasForeignKey( profile => profile.GroupId )
                .HasConstraintName( "fk_student_profiles_groups_group_id" ).OnDelete( DeleteBehavior.Restrict );
        }
    }

    public sealed class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
    {
        public void Configure( EntityTypeBuilder<StaffProfile> builder )
        {
            builder.ToTable( "staff_profiles" );
            builder.Property( profile => profile.FullName ).HasMaxLength( 200 );
            builder.Property( profile => profile.Position ).HasMaxLength( 100 );
            builder.Property( profile => profile.Office ).HasMaxLength( 32 );
            builder.HasIndex( profile => profile.UserId ).IsUnique();
            builder.HasOne<AppUser>().WithOne().HasForeignKey<StaffProfile>( profile => profile.UserId ).OnDelete( DeleteBehavior.Cascade );
            builder.HasOne( profile => profile.Faculty ).WithMany().HasForeignKey( profile => profile.FacultyId ).OnDelete( DeleteBehavior.Restrict );
        }
    }
}
