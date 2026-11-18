using Dekanat.Api.Features.Schedule;
using static System.DayOfWeek;
using static Dekanat.Api.Features.Schedule.LessonType;
using static Dekanat.Api.Infrastructure.Seed.WeekParity;

namespace Dekanat.Api.Infrastructure.Seed
{
    /// <summary>
    /// Вымышленные данные для демо. Все люди придуманы, совпадения с реальными людьми случайны. Как их получали,
    /// записано в docs/ai/prompts/seed-demo-data.md.
    /// </summary>
    public static class DemoData
    {
        public sealed record DemoProgram( string Faculty, string Code, string Name );

        public sealed record DemoGroup( string Name, string ProgramCode, int Course );

        public sealed record DemoStudent( string Login, string FullName, string Group, string GradeBookNumber );

        public sealed record DemoStaff( string Login, string FullName, string Faculty, string Position, string Office );

        public sealed record DemoTeacher( string FullName, string Department, string Position )
        {
            public string Surname => FullName.Split( ' ' )[ 0 ];
        }

        /// <summary>Строка шаблона недели. Преподаватель указан фамилией, они в справочнике не повторяются.</summary>
        public sealed record DemoLesson( string Group, DayOfWeek Day, int Slot, WeekParity Weeks, string Discipline, LessonType Type, string Teacher, string Room );

        public enum Pick
        {
            First,
            Second,
            Last,
        }

        /// <summary>
        /// Изменение, которое сид вносит в ближайшие дни от даты запуска, чтобы перенос и отмену было видно сразу.
        /// StudyDay 0 значит первый учебный день группы после сегодняшнего.
        /// </summary>
        public sealed record DemoChange(
            string Group,
            int StudyDay,
            Pick Lesson,
            LessonChangeType Type,
            string Reason,
            int HoursAgo,
            int? NewSlot = null,
            string? NewRoom = null );

        public const string Informatics = "Факультет информатики";
        public const string Economics = "Факультет экономики и управления";

        public static readonly string[] Faculties = [ Informatics, Economics ];

        public static readonly DemoProgram[] Programs =
        [
            new( Informatics, "09.03.04", "Программная инженерия" ),
            new( Informatics, "09.03.01", "Информатика и вычислительная техника" ),
            new( Economics, "38.03.05", "Бизнес-информатика" ),
        ];

        public static readonly DemoGroup[] Groups =
        [
            new( "ПИ-21", "09.03.04", 1 ),
            new( "ИВТ-23", "09.03.01", 2 ),
            new( "БИ-31", "38.03.05", 3 ),
        ];

        public static readonly DemoStudent[] Students =
        [
            new( "a.ivanova", "Иванова Анастасия Сергеевна", "ПИ-21", "2026-0123" ),
            new( "m.markov", "Марков Марк Андреевич", "ПИ-21", "2026-0147" ),
            new( "v.tikhonova", "Тихонова Вероника Павловна", "ПИ-21", "2026-0152" ),
            new( "t.guseynov", "Гусейнов Тимур Ринатович", "ПИ-21", "2026-0168" ),
            new( "g.lebedev", "Лебедев Глеб Олегович", "ИВТ-23", "2025-0311" ),
            new( "d.sokolova", "Соколова Дарья Игоревна", "ИВТ-23", "2025-0326" ),
            new( "k.akhmedova", "Ахмедова Камила Руслановна", "ИВТ-23", "2025-0339" ),
            new( "i.zakharov", "Захаров Илья Викторович", "БИ-31", "2024-0507" ),
            new( "u.polyakova", "Полякова Ульяна Денисовна", "БИ-31", "2024-0514" ),
        ];

        public static readonly DemoStaff[] StaffMembers =
        [
            new( "o.krylova", "Крылова Ольга Сергеевна", Informatics, "Специалист деканата", "204" ),
        ];

        public static readonly DemoTeacher[] Teachers =
        [
            new( "Беляев Арсений Константинович", "Кафедра высшей математики", "Доцент" ),
            new( "Ермакова Лидия Петровна", "Кафедра высшей математики", "Старший преподаватель" ),
            new( "Фадеев Кирилл Романович", "Кафедра дискретной математики", "Ассистент" ),
            new( "Шубин Роман Евгеньевич", "Кафедра физики", "Доцент" ),
            new( "Карпенко Наталья Владимировна", "Кафедра программной инженерии", "Профессор" ),
            new( "Мирошников Степан Аркадьевич", "Кафедра программной инженерии", "Старший преподаватель" ),
            new( "Зуева Олеся Геннадьевна", "Кафедра вычислительной техники", "Доцент" ),
            new( "Дьяконов Валерий Игоревич", "Кафедра вычислительной техники", "Доцент" ),
            new( "Осипова Маргарита Львовна", "Кафедра иностранных языков", "Преподаватель" ),
            new( "Румянцева Елена Борисовна", "Кафедра истории", "Доцент" ),
            new( "Чернов Аркадий Михайлович", "Кафедра экономической теории", "Доцент" ),
            new( "Субботина Инна Аркадьевна", "Кафедра бизнес-информатики", "Старший преподаватель" ),
        ];

        public static readonly string[] Disciplines =
        [
            "Математический анализ",
            "Линейная алгебра",
            "Дискретная математика",
            "Физика",
            "Основы программирования",
            "Алгоритмы и структуры данных",
            "Базы данных",
            "Операционные системы",
            "Английский язык",
            "История России",
            "Микроэкономика",
            "Моделирование бизнес-процессов",
        ];

        public static readonly DemoLesson[] Week =
        [
            // ПИ-21, 1 курс. В субботу пар нет.
            new( "ПИ-21", Monday, 1, Every, "Математический анализ", Lecture, "Беляев", "101" ),
            new( "ПИ-21", Monday, 2, Every, "Математический анализ", Practice, "Беляев", "305" ),
            new( "ПИ-21", Monday, 3, Odd, "Английский язык", Practice, "Осипова", "412" ),
            new( "ПИ-21", Tuesday, 2, Every, "Основы программирования", Lecture, "Карпенко", "101" ),
            new( "ПИ-21", Tuesday, 3, Every, "Основы программирования", Lab, "Карпенко", "410" ),
            new( "ПИ-21", Tuesday, 4, Even, "История России", Lecture, "Румянцева", "118" ),
            new( "ПИ-21", Wednesday, 1, Every, "Линейная алгебра", Lecture, "Ермакова", "101" ),
            new( "ПИ-21", Wednesday, 2, Every, "Линейная алгебра", Practice, "Ермакова", "214" ),
            new( "ПИ-21", Wednesday, 3, Odd, "Физика", Lecture, "Шубин", "120" ),
            new( "ПИ-21", Thursday, 2, Every, "Дискретная математика", Lecture, "Фадеев", "118" ),
            new( "ПИ-21", Thursday, 3, Every, "Дискретная математика", Practice, "Фадеев", "307" ),
            new( "ПИ-21", Thursday, 4, Even, "Физика", Lab, "Шубин", "220" ),
            new( "ПИ-21", Friday, 1, Every, "Английский язык", Practice, "Осипова", "412" ),
            new( "ПИ-21", Friday, 2, Odd, "История России", Practice, "Румянцева", "310" ),
            new( "ПИ-21", Friday, 3, Even, "Физика", Practice, "Шубин", "305" ),

            // ИВТ-23, 2 курс. Суббота по нечётным неделям.
            new( "ИВТ-23", Monday, 2, Every, "Алгоритмы и структуры данных", Lecture, "Мирошников", "120" ),
            new( "ИВТ-23", Monday, 3, Every, "Алгоритмы и структуры данных", Lab, "Мирошников", "410" ),
            new( "ИВТ-23", Tuesday, 1, Every, "Базы данных", Lecture, "Зуева", "118" ),
            new( "ИВТ-23", Tuesday, 2, Odd, "Базы данных", Lab, "Зуева", "409" ),
            new( "ИВТ-23", Tuesday, 2, Even, "Операционные системы", Lab, "Дьяконов", "409" ),
            new( "ИВТ-23", Wednesday, 2, Every, "Операционные системы", Lecture, "Дьяконов", "120" ),
            new( "ИВТ-23", Wednesday, 3, Every, "Английский язык", Practice, "Осипова", "414" ),
            new( "ИВТ-23", Wednesday, 4, Odd, "Дискретная математика", Practice, "Фадеев", "307" ),
            new( "ИВТ-23", Thursday, 1, Every, "Физика", Lecture, "Шубин", "120" ),
            new( "ИВТ-23", Thursday, 2, Even, "Физика", Lab, "Шубин", "220" ),
            new( "ИВТ-23", Thursday, 3, Every, "Математический анализ", Practice, "Беляев", "214" ),
            new( "ИВТ-23", Friday, 2, Every, "Алгоритмы и структуры данных", Practice, "Мирошников", "307" ),
            new( "ИВТ-23", Friday, 3, Odd, "Базы данных", Practice, "Зуева", "305" ),
            new( "ИВТ-23", Saturday, 2, Odd, "История России", Lecture, "Румянцева", "101" ),

            // БИ-31, 3 курс. Четверг свободный.
            new( "БИ-31", Monday, 3, Every, "Микроэкономика", Lecture, "Чернов", "202" ),
            new( "БИ-31", Monday, 4, Every, "Микроэкономика", Practice, "Чернов", "206" ),
            new( "БИ-31", Tuesday, 2, Every, "Моделирование бизнес-процессов", Lecture, "Субботина", "202" ),
            new( "БИ-31", Tuesday, 3, Every, "Моделирование бизнес-процессов", Lab, "Субботина", "411" ),
            new( "БИ-31", Wednesday, 1, Even, "Английский язык", Practice, "Осипова", "416" ),
            new( "БИ-31", Wednesday, 2, Every, "Базы данных", Lecture, "Зуева", "118" ),
            new( "БИ-31", Wednesday, 3, Odd, "Базы данных", Lab, "Зуева", "409" ),
            new( "БИ-31", Friday, 3, Every, "Моделирование бизнес-процессов", Practice, "Субботина", "206" ),
            new( "БИ-31", Friday, 4, Odd, "Микроэкономика", Practice, "Чернов", "202" ),
        ];

        /// <summary>
        /// У ПИ-21 перенос, другая аудитория и отмена, у ИВТ-23 перенос и аудитория, у БИ-31 всё по плану.
        /// Аудитории 501 и 502 и шестая пара в шаблоне не заняты, поэтому изменения ни с чем не пересекаются.
        /// </summary>
        public static readonly DemoChange[] Changes =
        [
            new( "ПИ-21", 0, Pick.First, LessonChangeType.Reschedule, "Преподаватель на заседании учёного совета", 20, NewSlot: 6, NewRoom: "305" ),
            new( "ПИ-21", 0, Pick.Second, LessonChangeType.RoomChange, "В аудитории ремонтируют проектор", 5, NewRoom: "501" ),
            new( "ПИ-21", 1, Pick.Last, LessonChangeType.Cancellation, "Преподаватель на больничном", 2 ),
            new( "ИВТ-23", 0, Pick.First, LessonChangeType.RoomChange, "Аудиторию заняли под олимпиаду", 26, NewRoom: "502" ),
            new( "ИВТ-23", 1, Pick.First, LessonChangeType.Reschedule, "Совпало с конференцией кафедры", 8, NewSlot: 6, NewRoom: "214" ),
        ];
    }
}
