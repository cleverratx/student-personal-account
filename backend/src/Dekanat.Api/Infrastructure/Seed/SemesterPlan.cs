namespace Dekanat.Api.Infrastructure.Seed
{
    public enum WeekParity
    {
        Every,

        /// <summary>Нечётная неделя. Первая неделя семестра нечётная.</summary>
        Odd,

        Even,
    }

    /// <summary>Учебный семестр: первый и последний день занятий.</summary>
    public sealed record Semester( DateOnly Start, DateOnly End )
    {
        /// <summary>
        /// Семестр, который идёт в эту дату, а на каникулах и в сессию ближайший следующий: осенний с 1 сентября
        /// по 28 декабря, весенний с 9 февраля по 31 мая. Так у демо-данных всегда есть пары впереди.
        /// </summary>
        public static Semester Containing( DateOnly date )
        {
            Semester[] candidates = [ Spring( date.Year ), Autumn( date.Year ), Spring( date.Year + 1 ) ];
            return candidates.First( semester => semester.End >= date );
        }

        private static Semester Spring( int year )
        {
            return new Semester( new DateOnly( year, 2, 9 ), new DateOnly( year, 5, 31 ) );
        }

        private static Semester Autumn( int year )
        {
            return new Semester( new DateOnly( year, 9, 1 ), new DateOnly( year, 12, 28 ) );
        }

        /// <summary>Номер учебной недели, считая с недели, где первый день семестра.</summary>
        public int WeekNumber( DateOnly date )
        {
            return ( ( MondayOf( date ).DayNumber - MondayOf( Start ).DayNumber ) / 7 ) + 1;
        }

        public WeekParity ParityOf( DateOnly date )
        {
            return WeekNumber( date ) % 2 == 1 ? WeekParity.Odd : WeekParity.Even;
        }

        private static DateOnly MondayOf( DateOnly date )
        {
            int sinceMonday = ( ( int )date.DayOfWeek + 6 ) % 7;
            return date.AddDays( -sinceMonday );
        }
    }

    /// <summary>
    /// Расписание семестра из шаблона недели: каждая строка шаблона говорит, в какой день, какой парой
    /// и по каким неделям идёт занятие. Воскресенья и праздники пропускаются.
    /// </summary>
    public static class SemesterPlan
    {
        /// <summary>Звонки по времени вуза.</summary>
        public static readonly IReadOnlyDictionary<int, (TimeOnly Start, TimeOnly End)> Slots = new Dictionary<int, (TimeOnly, TimeOnly)>
        {
            [ 1 ] = (new TimeOnly( 9, 0 ), new TimeOnly( 10, 30 )),
            [ 2 ] = (new TimeOnly( 10, 40 ), new TimeOnly( 12, 10 )),
            [ 3 ] = (new TimeOnly( 12, 20 ), new TimeOnly( 13, 50 )),
            [ 4 ] = (new TimeOnly( 14, 30 ), new TimeOnly( 16, 0 )),
            [ 5 ] = (new TimeOnly( 16, 10 ), new TimeOnly( 17, 40 )),
            [ 6 ] = (new TimeOnly( 17, 50 ), new TimeOnly( 19, 20 )),
        };

        /// <summary>Нерабочие праздничные дни, когда пар нет.</summary>
        private static readonly HashSet<(int Month, int Day)> Holidays = [ (1, 7), (2, 23), (3, 8), (5, 1), (5, 9), (6, 12), (11, 4) ];

        public static bool IsHoliday( DateOnly date )
        {
            return Holidays.Contains( (date.Month, date.Day) );
        }

        public static IEnumerable<(DateOnly Date, T Entry)> Expand<T>(
            Semester semester,
            IReadOnlyCollection<T> template,
            Func<T, DayOfWeek> day,
            Func<T, WeekParity> weeks )
        {
            for ( DateOnly date = semester.Start; date <= semester.End; date = date.AddDays( 1 ) )
            {
                if ( date.DayOfWeek == DayOfWeek.Sunday || IsHoliday( date ) )
                {
                    continue;
                }

                WeekParity parity = semester.ParityOf( date );
                foreach ( T entry in template )
                {
                    if ( day( entry ) == date.DayOfWeek && ( weeks( entry ) == WeekParity.Every || weeks( entry ) == parity ) )
                    {
                        yield return (date, entry);
                    }
                }
            }
        }
    }
}
