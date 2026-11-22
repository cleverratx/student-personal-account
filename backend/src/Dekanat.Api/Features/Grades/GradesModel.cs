using Dekanat.Api.Features.Profile;
using Dekanat.Api.Features.Schedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Dekanat.Api.Features.Schedule.LessonConfiguration;

namespace Dekanat.Api.Features.Grades
{
    /// <summary>
    /// Форма контроля по дисциплине в семестре.
    /// </summary>
    public enum ControlForm
    {
        Exam,           // Экзамен
        Credit,         // Зачёт
        GradedCredit,   // Дифференцированный зачёт
        CourseWork,     // Курсовая работа
    }

    /// <summary>
    /// Статус прохождения аттестации.
    /// </summary>
    public enum AssessmentStatus
    {
        Passed,         // Сдано
        Failed,         // Не сдано
        NotAdmitted,    // Не допущен
        Debt,           // Долг (пересдача)
    }

    /// <summary>
    /// Запись об аттестации студента по дисциплине в семестре.
    /// Одна строка соответствует одной форме контроля (например, экзамен по матану в 1-м семестре).
    /// </summary>
    public sealed class Assessment
    {
        public Guid Id { get; set; }

        public Guid StudentProfileId { get; set; }

        public StudentProfile StudentProfile { get; set; } = null!;

        public Guid DisciplineId { get; set; }

        public Discipline Discipline { get; set; } = null!;

        /// <summary>Номер семестра, начиная с 1.</summary>
        public int Semester { get; set; }

        public ControlForm ControlForm { get; set; }

        /// <summary>
        /// Оценка: 5, 4, 3 или 2 для экзамена и дифзачёта; null для зачёта и курсовой.
        /// </summary>
        public int? Grade { get; set; }

        /// <summary>
        /// Зачтено: true/false для зачёта и курсовой; null для экзамена и дифзачёта.
        /// </summary>
        public bool? IsPassed { get; set; }

        /// <summary>Баллы БРС (если применяется). Может отсутствовать.</summary>
        public int? Points { get; set; }

        /// <summary>Дата аттестации. Null, если ещё не проходила.</summary>
        public DateOnly? Date { get; set; }

        public AssessmentStatus Status { get; set; }
    }

    /// <summary>
    /// Событие сессии: экзамен, зачёт или консультация, привязанные к конкретной паре в расписании.
    /// </summary>
    public sealed class SessionEvent
    {
        public Guid Id { get; set; }

        public Guid GroupId { get; set; }

        public StudyGroup Group { get; set; } = null!;

        public Guid DisciplineId { get; set; }

        public Discipline Discipline { get; set; } = null!;

        public Guid TeacherId { get; set; }

        public Teacher Teacher { get; set; } = null!;

        public SessionEventType Type { get; set; }

        /// <summary>Дата и время начала, UTC.</summary>
        public DateTime StartsAt { get; set; }

        /// <summary>Дата и время окончания, UTC.</summary>
        public DateTime EndsAt { get; set; }

        public required string Room { get; set; }

        /// <summary>Семестр, к которому относится сессия.</summary>
        public int Semester { get; set; }
    }

    public enum SessionEventType
    {
        Exam,           // Экзамен
        Credit,         // Зачёт
        GradedCredit,   // Дифзачёт
        Consultation,   // Консультация
    }

    public sealed class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
    {
        public void Configure( EntityTypeBuilder<Assessment> builder )
        {
            builder.ToTable( "assessments", table =>
            {
                table.HasCheckConstraint( "ck_assessments_control_form", SqlIn( "control_form", Enum.GetNames<ControlForm>() ) );
                table.HasCheckConstraint( "ck_assessments_status", SqlIn( "status", Enum.GetNames<AssessmentStatus>() ) );
                table.HasCheckConstraint( "ck_assessments_semester_positive", "semester > 0" );
                table.HasCheckConstraint( "ck_assessments_grade_range", "grade IS NULL OR (grade >= 2 AND grade <= 5)" );
                table.HasCheckConstraint( "ck_assessments_points_range", "points IS NULL OR (points >= 0 AND points <= 100)" );
            } );

            builder.Property( a => a.ControlForm ).HasConversion<string>().HasMaxLength( 16 );
            builder.Property( a => a.Status ).HasConversion<string>().HasMaxLength( 16 );

            // Уникальность: один студент — одна форма контроля по дисциплине в семестре.
            builder.HasIndex( a => new { a.StudentProfileId, a.DisciplineId, a.Semester, a.ControlForm } ).IsUnique();
            // Выборка успеваемости студента за семестр.
            builder.HasIndex( a => new { a.StudentProfileId, a.Semester } );

            builder.HasOne( a => a.StudentProfile ).WithMany()
                .HasForeignKey( a => a.StudentProfileId )
                .HasConstraintName( "fk_assessments_student_profiles_student_profile_id" )
                .OnDelete( DeleteBehavior.Cascade );

            builder.HasOne( a => a.Discipline ).WithMany()
                .HasForeignKey( a => a.DisciplineId )
                .HasConstraintName( "fk_assessments_disciplines_discipline_id" )
                .OnDelete( DeleteBehavior.Restrict );
        }
    }

    public sealed class SessionEventConfiguration : IEntityTypeConfiguration<SessionEvent>
    {
        public void Configure( EntityTypeBuilder<SessionEvent> builder )
        {
            builder.ToTable( "session_events", table =>
            {
                table.HasCheckConstraint( "ck_session_events_type", SqlIn( "type", Enum.GetNames<SessionEventType>() ) );
                table.HasCheckConstraint( "ck_session_events_ends_after_starts", "ends_at > starts_at" );
                table.HasCheckConstraint( "ck_session_events_semester_positive", "semester > 0" );
            } );

            builder.Property( e => e.Type ).HasConversion<string>().HasMaxLength( 16 );
            builder.Property( e => e.Room ).HasMaxLength( 32 );

            builder.HasIndex( e => new { e.GroupId, e.Semester, e.StartsAt } );

            builder.HasOne( e => e.Group ).WithMany()
                .HasForeignKey( e => e.GroupId )
                .HasConstraintName( "fk_session_events_groups_group_id" )
                .OnDelete( DeleteBehavior.Restrict );

            builder.HasOne( e => e.Discipline ).WithMany()
                .HasForeignKey( e => e.DisciplineId )
                .HasConstraintName( "fk_session_events_disciplines_discipline_id" )
                .OnDelete( DeleteBehavior.Restrict );

            builder.HasOne( e => e.Teacher ).WithMany()
                .HasForeignKey( e => e.TeacherId )
                .HasConstraintName( "fk_session_events_teachers_teacher_id" )
                .OnDelete( DeleteBehavior.Restrict );
        }
    }
}
