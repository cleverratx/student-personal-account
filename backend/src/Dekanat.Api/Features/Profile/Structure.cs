using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dekanat.Api.Features.Profile
{
    // Учебная структура: факультет, направление, группа. Курс студента и факультет берутся через группу.

    public sealed class Faculty
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }
    }

    public sealed class StudyProgram
    {
        public Guid Id { get; set; }

        public Guid FacultyId { get; set; }

        public Faculty Faculty { get; set; } = null!;

        /// <summary>Код направления, например 09.03.04.</summary>
        public required string Code { get; set; }

        public required string Name { get; set; }
    }

    public sealed class StudyGroup
    {
        public Guid Id { get; set; }

        public Guid ProgramId { get; set; }

        public StudyProgram Program { get; set; } = null!;

        public required string Name { get; set; }

        public int Course { get; set; }
    }

    public sealed class FacultyConfiguration : IEntityTypeConfiguration<Faculty>
    {
        public void Configure( EntityTypeBuilder<Faculty> builder )
        {
            builder.ToTable( "faculties" );
            builder.Property( faculty => faculty.Name ).HasMaxLength( 200 );
            builder.HasIndex( faculty => faculty.Name ).IsUnique();
        }
    }

    public sealed class StudyProgramConfiguration : IEntityTypeConfiguration<StudyProgram>
    {
        public void Configure( EntityTypeBuilder<StudyProgram> builder )
        {
            builder.ToTable( "programs" );
            builder.Property( program => program.Code ).HasMaxLength( 16 );
            builder.Property( program => program.Name ).HasMaxLength( 200 );
            builder.HasIndex( program => new { program.FacultyId, program.Code } ).IsUnique();
            builder.HasOne( program => program.Faculty ).WithMany().HasForeignKey( program => program.FacultyId ).OnDelete( DeleteBehavior.Restrict );
        }
    }

    public sealed class StudyGroupConfiguration : IEntityTypeConfiguration<StudyGroup>
    {
        public void Configure( EntityTypeBuilder<StudyGroup> builder )
        {
            builder.ToTable( "groups", table => table.HasCheckConstraint( "ck_groups_course", "course BETWEEN 1 AND 6" ) );
            builder.Property( group => group.Name ).HasMaxLength( 32 );
            builder.HasIndex( group => group.Name ).IsUnique();
            builder.HasOne( group => group.Program ).WithMany().HasForeignKey( group => group.ProgramId )
                .HasConstraintName( "fk_groups_programs_program_id" ).OnDelete( DeleteBehavior.Restrict );
        }
    }
}
