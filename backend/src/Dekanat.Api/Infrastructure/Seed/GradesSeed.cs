using Dekanat.Api.Features.Grades;
using Dekanat.Api.Features.Profile;
using Dekanat.Api.Features.Schedule;
using Dekanat.Api.Infrastructure.Persistence;
using Dekanat.Api.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using static Dekanat.Api.Features.Grades.AssessmentStatus;
using static Dekanat.Api.Features.Grades.ControlForm;
using static Dekanat.Api.Features.Grades.SessionEventType;

namespace Dekanat.Api.Infrastructure.Seed
{
    /// <summary>
    /// Вымышленные оценки и расписание сессии. Добавляется к основному DemoSeeder.
    /// Идемпотентно: если у студента уже есть записи об аттестации, сид не трогает их.
    /// </summary>
    public static class GradesSeed
    {
        /// <summary>
        /// Текущий семестр в демо-данных.
        /// </summary>
        private const int CurrentSemester = 2;

        public static async Task SeedGradesAsync( AppDbContext db, UniversityClock clock, ILogger logger )
        {
            // Пропускаем, если оценки уже есть.
            if ( await db.Set<Assessment>().AnyAsync() )
            {
                return;
            }

            Dictionary<string, StudentProfile> students = await db.Set<StudentProfile>()
                .Include( p => p.Group )
                .ToDictionaryAsync( p => p.Group.Name + ":" + p.FullName );

            Dictionary<string, Discipline> disciplines = await db.Set<Discipline>()
                .ToDictionaryAsync( d => d.Name );

            Dictionary<string, StudyGroup> groups = await db.Set<StudyGroup>()
                .ToDictionaryAsync( g => g.Name );

            Dictionary<string, Teacher> teachers = await db.Set<Teacher>()
                .ToDictionaryAsync( t => t.FullName.Split( ' ' )[ 0 ] ); // по фамилии

            // ── Оценки за прошлые семестры и текущий ─────────────────────────

            List<Assessment> assessments =
            [
                // ── ПИ-21, Иванова (семестр 1 — все сдано) ──────────────────
                Make( students, disciplines, "ПИ-21:Иванова Анастасия Сергеевна", "Математический анализ",   1, Exam,         grade: 5, points: 87, date: "2026-01-15", status: Passed ),
                Make( students, disciplines, "ПИ-21:Иванова Анастасия Сергеевна", "Линейная алгебра",        1, GradedCredit, grade: 4, points: 74, date: "2026-01-18", status: Passed ),
                Make( students, disciplines, "ПИ-21:Иванова Анастасия Сергеевна", "Основы программирования", 1, Exam,         grade: 5, points: 94, date: "2026-01-20", status: Passed ),
                Make( students, disciplines, "ПИ-21:Иванова Анастасия Сергеевна", "Английский язык",         1, Credit,       isPassed: true,         date: "2026-01-12", status: Passed ),
                Make( students, disciplines, "ПИ-21:Иванова Анастасия Сергеевна", "История России",          1, Credit,       isPassed: true,         date: "2026-01-10", status: Passed ),

                // ── ПИ-21, Иванова (семестр 2 — текущий, долга нет) ─────────
                Make( students, disciplines, "ПИ-21:Иванова Анастасия Сергеевна", "Математический анализ",   2, Exam,   null, null,   null,  Failed ),
                Make( students, disciplines, "ПИ-21:Иванова Анастасия Сергеевна", "Дискретная математика",   2, Exam,   null, null,   null,  Failed ),
                Make( students, disciplines, "ПИ-21:Иванова Анастасия Сергеевна", "Основы программирования", 2, Credit, null, null,   null,  Failed ),
                Make( students, disciplines, "ПИ-21:Иванова Анастасия Сергеевна", "Физика",                  2, Credit, null, null,   null,  Failed ),

                // ── ПИ-21, Марков (семестр 1 — долг по физике) ──────────────
                Make( students, disciplines, "ПИ-21:Марков Марк Андреевич", "Математический анализ",   1, Exam,         grade: 4, points: 68, date: "2026-01-15", status: Passed ),
                Make( students, disciplines, "ПИ-21:Марков Марк Андреевич", "Линейная алгебра",        1, GradedCredit, grade: 3, points: 55, date: "2026-01-18", status: Passed ),
                Make( students, disciplines, "ПИ-21:Марков Марк Андреевич", "Основы программирования", 1, Exam,         grade: 4, points: 72, date: "2026-01-20", status: Passed ),
                Make( students, disciplines, "ПИ-21:Марков Марк Андреевич", "Английский язык",         1, Credit,       isPassed: true,         date: "2026-01-12", status: Passed ),
                Make( students, disciplines, "ПИ-21:Марков Марк Андреевич", "История России",          1, Credit,       isPassed: false,        date: "2026-01-10", status: Debt ),

                // ── ПИ-21, Марков (семестр 2 — текущий, не допущен к дискретной) ─
                Make( students, disciplines, "ПИ-21:Марков Марк Андреевич", "Математический анализ",   2, Exam,   null, null, null, Failed ),
                Make( students, disciplines, "ПИ-21:Марков Марк Андреевич", "Дискретная математика",   2, Exam,   null, null, null, NotAdmitted ),
                Make( students, disciplines, "ПИ-21:Марков Марк Андреевич", "Основы программирования", 2, Credit, null, null, null, Failed ),
                Make( students, disciplines, "ПИ-21:Марков Марк Андреевич", "Физика",                  2, Credit, null, null, null, Failed ),

                // ── ИВТ-23, Лебедев (семестр 2 — уже прошёл, все сдано) ─────
                Make( students, disciplines, "ИВТ-23:Лебедев Глеб Олегович", "Алгоритмы и структуры данных", 2, Exam,         grade: 5, points: 91, date: "2026-01-14", status: Passed ),
                Make( students, disciplines, "ИВТ-23:Лебедев Глеб Олегович", "Базы данных",                  2, GradedCredit, grade: 5, points: 88, date: "2026-01-16", status: Passed ),
                Make( students, disciplines, "ИВТ-23:Лебедев Глеб Олегович", "Операционные системы",          2, Exam,         grade: 4, points: 79, date: "2026-01-19", status: Passed ),
                Make( students, disciplines, "ИВТ-23:Лебедев Глеб Олегович", "Английский язык",               2, Credit,       isPassed: true,         date: "2026-01-11", status: Passed ),

                // ── ИВТ-23, Лебедев (семестр 3 — текущий) ───────────────────
                Make( students, disciplines, "ИВТ-23:Лебедев Глеб Олегович", "Алгоритмы и структуры данных", 3, Exam,         null, null, null, Failed ),
                Make( students, disciplines, "ИВТ-23:Лебедев Глеб Олегович", "Операционные системы",          3, GradedCredit, null, null, null, Failed ),
                Make( students, disciplines, "ИВТ-23:Лебедев Глеб Олегович", "Математический анализ",         3, Credit,       null, null, null, Failed ),

                // ── БИ-31, Захаров (семестр 4 — один долг) ──────────────────
                Make( students, disciplines, "БИ-31:Захаров Илья Викторович", "Микроэкономика",             4, Exam,         grade: 3, points: 51, date: "2026-01-13", status: Passed ),
                Make( students, disciplines, "БИ-31:Захаров Илья Викторович", "Моделирование бизнес-процессов", 4, GradedCredit, grade: 2, date: "2026-01-17", status: Debt ),
                Make( students, disciplines, "БИ-31:Захаров Илья Викторович", "Английский язык",             4, Credit,       isPassed: true,         date: "2026-01-09", status: Passed ),
            ];

            db.Set<Assessment>().AddRange( assessments );

            // ── Расписание сессии ─────────────────────────────────────────────
            // Сессия: последняя неделя января — первая февраля.
            // Используем UTC, реальное время подберём через UniversityClock.
            DateTime SessionUtc( string date, int hour ) =>
                clock.ToUtc( DateOnly.Parse( date ), new TimeOnly( hour, 0 ) );

            if ( groups.TryGetValue( "ПИ-21", out StudyGroup? pi21 ) )
            {
                db.Set<SessionEvent>().AddRange(
                    // Консультации
                    new SessionEvent {
                        Id = Guid.NewGuid(),
                        Group = pi21,
                        Semester = CurrentSemester,
                        Discipline = disciplines[ "Математический анализ" ],
                        Teacher = teachers[ "Беляев" ],
                        Type = Consultation,
                        StartsAt = SessionUtc( "2027-01-25", 10 ),
                        EndsAt = SessionUtc( "2027-01-25", 12 ),
                        Room = "214",
                    },
                    new SessionEvent {
                        Id = Guid.NewGuid(),
                        Group = pi21,
                        Semester = CurrentSemester,
                        Discipline = disciplines[ "Дискретная математика" ],
                        Teacher = teachers[ "Фадеев" ],
                        Type = Consultation,
                        StartsAt = SessionUtc( "2027-01-27", 14 ),
                        EndsAt = SessionUtc( "2027-01-27", 16 ),
                        Room = "307",
                    },
                    // Зачёты
                    new SessionEvent {
                        Id = Guid.NewGuid(),
                        Group = pi21,
                        Semester = CurrentSemester,
                        Discipline = disciplines[ "Основы программирования" ],
                        Teacher = teachers[ "Карпенко" ],
                        Type = Credit,
                        StartsAt = SessionUtc( "2027-01-28", 10 ),
                        EndsAt = SessionUtc( "2027-01-28", 12 ),
                        Room = "101",
                    },
                    new SessionEvent {
                        Id = Guid.NewGuid(),
                        Group = pi21,
                        Semester = CurrentSemester,
                        Discipline = disciplines[ "Физика" ],
                        Teacher = teachers[ "Шубин" ],
                        Type = Credit,
                        StartsAt = SessionUtc( "2027-01-29", 10 ),
                        EndsAt = SessionUtc( "2027-01-29", 12 ),
                        Room = "120",
                    },
                    // Экзамены
                    new SessionEvent {
                        Id = Guid.NewGuid(),
                        Group = pi21,
                        Semester = CurrentSemester,
                        Discipline = disciplines[ "Математический анализ" ],
                        Teacher = teachers[ "Беляев" ],
                        Type = Exam,
                        StartsAt = SessionUtc( "2027-02-03", 10 ),
                        EndsAt = SessionUtc( "2027-02-03", 13 ),
                        Room = "101",
                    },
                    new SessionEvent {
                        Id = Guid.NewGuid(),
                        Group = pi21,
                        Semester = CurrentSemester,
                        Discipline = disciplines[ "Дискретная математика" ],
                        Teacher = teachers[ "Фадеев" ],
                        Type = Exam,
                        StartsAt = SessionUtc( "2027-02-07", 10 ),
                        EndsAt = SessionUtc( "2027-02-07", 13 ),
                        Room = "118",
                    } );
            }

            await db.SaveChangesAsync();
            logger.LogInformation( "Демо-данные успеваемости: {Count} записей об аттестации", assessments.Count );
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Assessment Make(
            Dictionary<string, StudentProfile> students,
            Dictionary<string, Discipline> disciplines,
            string studentKey,
            string disciplineName,
            int semester,
            ControlForm controlForm,
            int? grade = null,
            int? points = null,
            string? date = null,
            AssessmentStatus status = Failed,
            bool? isPassed = null )
        {
            return new Assessment
            {
                Id = Guid.NewGuid(),
                StudentProfile = students[ studentKey ],
                Discipline = disciplines[ disciplineName ],
                Semester = semester,
                ControlForm = controlForm,
                Grade = grade,
                IsPassed = isPassed,
                Points = points,
                Date = date is not null ? DateOnly.Parse( date ) : null,
                Status = status,
            };
        }
    }
}
