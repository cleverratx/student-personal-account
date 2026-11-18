using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dekanat.Api.Features.Schedule
{
    public enum LessonType
    {
        Lecture,
        Practice,
        Lab,
        Consultation,
        Exam,
        Credit,
        GradedCredit,
    }

    public enum LessonChangeType
    {
        Reschedule,
        RoomChange,
        Cancellation,
    }

    /// <summary>
    /// Преподаватель. Это справочник для расписания, учётной записи у преподавателя нет.
    /// </summary>
    public sealed class Teacher
    {
        public Guid Id { get; set; }

        public required string FullName { get; set; }

        public required string Department { get; set; }

        public required string Position { get; set; }
    }

    public sealed class Discipline
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }
    }

    /// <summary>
    /// Пара в текущем виде: время, аудитория и признак отмены уже с учётом изменений. Что было до изменения,
    /// хранит <see cref="LessonChange"/>. Время в UTC.
    /// </summary>
    public sealed class Lesson
    {
        public Guid Id { get; set; }

        public Guid GroupId { get; set; }

        public StudyGroup Group { get; set; } = null!;

        public Guid DisciplineId { get; set; }

        public Discipline Discipline { get; set; } = null!;

        public Guid TeacherId { get; set; }

        public Teacher Teacher { get; set; } = null!;

        public DateTime StartsAt { get; set; }

        public DateTime EndsAt { get; set; }

        public required string Room { get; set; }

        public LessonType Type { get; set; }

        public bool IsCancelled { get; set; }

        public List<LessonChange> Changes { get; set; } = [];
    }

    /// <summary>
    /// Одно изменение пары: перенос, другая аудитория или отмена. Время в UTC.
    /// </summary>
    public sealed class LessonChange
    {
        public Guid Id { get; set; }

        public Guid LessonId { get; set; }

        public LessonChangeType Type { get; set; }

        public DateTime? OldStartsAt { get; set; }

        public DateTime? NewStartsAt { get; set; }

        public string? OldRoom { get; set; }

        public string? NewRoom { get; set; }

        public string? Reason { get; set; }

        public DateTime ChangedAt { get; set; }

        /// <summary>Кто из сотрудников внёс изменение. Пусто, если его учётную запись удалили.</summary>
        public Guid? ChangedByUserId { get; set; }
    }

    public sealed class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
    {
        public void Configure( EntityTypeBuilder<Teacher> builder )
        {
            builder.ToTable( "teachers" );
            builder.Property( teacher => teacher.FullName ).HasMaxLength( 200 );
            builder.Property( teacher => teacher.Department ).HasMaxLength( 200 );
            builder.Property( teacher => teacher.Position ).HasMaxLength( 100 );
        }
    }

    public sealed class DisciplineConfiguration : IEntityTypeConfiguration<Discipline>
    {
        public void Configure( EntityTypeBuilder<Discipline> builder )
        {
            builder.ToTable( "disciplines" );
            builder.Property( discipline => discipline.Name ).HasMaxLength( 200 );
            builder.HasIndex( discipline => discipline.Name ).IsUnique();
        }
    }

    public sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
    {
        public void Configure( EntityTypeBuilder<Lesson> builder )
        {
            builder.ToTable( "lessons", table =>
            {
                table.HasCheckConstraint( "ck_lessons_type", SqlIn( "type", Enum.GetNames<LessonType>() ) );
                table.HasCheckConstraint( "ck_lessons_ends_after_starts", "ends_at > starts_at" );
            } );
            builder.Property( lesson => lesson.Type ).HasConversion<string>().HasMaxLength( 16 );
            builder.Property( lesson => lesson.Room ).HasMaxLength( 32 );

            // Расписание группы, занятость преподавателя и аудитории при переносе.
            builder.HasIndex( lesson => new { lesson.GroupId, lesson.StartsAt } );
            builder.HasIndex( lesson => new { lesson.TeacherId, lesson.StartsAt } );
            builder.HasIndex( lesson => new { lesson.Room, lesson.StartsAt } );

            builder.HasOne( lesson => lesson.Group ).WithMany().HasForeignKey( lesson => lesson.GroupId )
                .HasConstraintName( "fk_lessons_groups_group_id" ).OnDelete( DeleteBehavior.Restrict );
            builder.HasOne( lesson => lesson.Discipline ).WithMany().HasForeignKey( lesson => lesson.DisciplineId )
                .HasConstraintName( "fk_lessons_disciplines_discipline_id" ).OnDelete( DeleteBehavior.Restrict );
            builder.HasOne( lesson => lesson.Teacher ).WithMany().HasForeignKey( lesson => lesson.TeacherId )
                .HasConstraintName( "fk_lessons_teachers_teacher_id" ).OnDelete( DeleteBehavior.Restrict );
        }

        internal static string SqlIn( string column, IEnumerable<string> values )
        {
            return $"{column} IN ({string.Join( ", ", values.Select( value => $"'{value}'" ) )})";
        }
    }

    public sealed class LessonChangeConfiguration : IEntityTypeConfiguration<LessonChange>
    {
        public void Configure( EntityTypeBuilder<LessonChange> builder )
        {
            builder.ToTable( "lesson_changes", table =>
                table.HasCheckConstraint( "ck_lesson_changes_type", LessonConfiguration.SqlIn( "type", Enum.GetNames<LessonChangeType>() ) ) );
            builder.Property( change => change.Type ).HasConversion<string>().HasMaxLength( 16 );
            builder.Property( change => change.OldRoom ).HasMaxLength( 32 );
            builder.Property( change => change.NewRoom ).HasMaxLength( 32 );
            builder.Property( change => change.Reason ).HasMaxLength( 300 );
            builder.HasIndex( change => new { change.LessonId, change.ChangedAt } );

            builder.HasOne<Lesson>().WithMany( lesson => lesson.Changes ).HasForeignKey( change => change.LessonId )
                .HasConstraintName( "fk_lesson_changes_lessons_lesson_id" ).OnDelete( DeleteBehavior.Cascade );
            builder.HasOne<AppUser>().WithMany().HasForeignKey( change => change.ChangedByUserId )
                .HasConstraintName( "fk_lesson_changes_users_changed_by_user_id" ).OnDelete( DeleteBehavior.SetNull );
        }
    }
}
