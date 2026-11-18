using Dekanat.Api.Features.Schedule;
using Dekanat.Api.Infrastructure.Seed;

namespace Dekanat.UnitTests.Schedule
{
    public sealed class ScheduleTests
    {
        private static readonly DateTime Morning = new( 2026, 11, 16, 7, 40, 0, DateTimeKind.Utc );
        private static readonly DateTime Evening = new( 2026, 11, 16, 13, 10, 0, DateTimeKind.Utc );

        [Theory]
        [InlineData( "Беляев Арсений Константинович", "Беляев А. К." )]
        [InlineData( "Ким Денис", "Ким Д." )]
        [InlineData( "Платон", "Платон" )]
        [InlineData( "  осипова   маргарита львовна ", "осипова М. Л." )]
        [InlineData( "", "" )]
        public void TeacherIsSurnameWithInitials( string fullName, string expected )
        {
            Assert.Equal( expected, ScheduleEndpoints.ShortName( fullName ) );
        }

        [Fact]
        public void NoChangesMeansNoChange()
        {
            Assert.Null( ScheduleEndpoints.SummarizeChanges( [], Morning, "214", false ) );
        }

        [Fact]
        public void SeveralChangesAreComparedWithOriginal()
        {
            // Сначала перенесли с утра на вечер, потом ещё раз сменили аудиторию.
            LessonChange[] changes =
            [
                new() { Type = LessonChangeType.Reschedule, OldStartsAt = Morning, NewStartsAt = Evening, OldRoom = "214", NewRoom = "305", Reason = "Первая причина", ChangedAt = Morning.AddDays( -2 ) },
                new() { Type = LessonChangeType.RoomChange, OldRoom = "305", NewRoom = "501", Reason = "Ремонт", ChangedAt = Morning.AddDays( -1 ) },
            ];

            LessonChangeDto? summary = ScheduleEndpoints.SummarizeChanges( changes, Evening, "501", false );

            Assert.NotNull( summary );
            Assert.Equal( LessonChangeType.Reschedule, summary.Type );
            Assert.Equal( Morning, summary.OldStartsAt );
            Assert.Equal( Evening, summary.NewStartsAt );
            Assert.Equal( "214", summary.OldRoom );
            Assert.Equal( "501", summary.NewRoom );
            Assert.Equal( "Ремонт", summary.Reason );
        }

        [Fact]
        public void ChangeThatWasUndoneIsNotShown()
        {
            LessonChange[] changes =
            [
                new() { Type = LessonChangeType.RoomChange, OldRoom = "214", NewRoom = "305", ChangedAt = Morning.AddDays( -2 ) },
                new() { Type = LessonChangeType.RoomChange, OldRoom = "305", NewRoom = "214", ChangedAt = Morning.AddDays( -1 ) },
            ];

            Assert.Null( ScheduleEndpoints.SummarizeChanges( changes, Morning, "214", false ) );
        }

        [Fact]
        public void CancellationWinsOverOtherChanges()
        {
            LessonChange[] changes =
            [
                new() { Type = LessonChangeType.RoomChange, OldRoom = "214", NewRoom = "305", ChangedAt = Morning.AddDays( -2 ) },
                new() { Type = LessonChangeType.Cancellation, Reason = "Преподаватель на больничном", ChangedAt = Morning.AddDays( -1 ) },
            ];

            LessonChangeDto? summary = ScheduleEndpoints.SummarizeChanges( changes, Morning, "305", true );

            Assert.Equal( LessonChangeType.Cancellation, summary?.Type );
            Assert.Equal( "Преподаватель на больничном", summary?.Reason );
        }
    }

    public sealed class SemesterPlanTests
    {
        [Theory]
        [InlineData( "2026-11-18", "2026-09-01", "2026-12-28" )]
        [InlineData( "2026-07-10", "2026-09-01", "2026-12-28" )]
        [InlineData( "2026-12-30", "2027-02-09", "2027-05-31" )]
        [InlineData( "2027-01-20", "2027-02-09", "2027-05-31" )]
        [InlineData( "2027-04-01", "2027-02-09", "2027-05-31" )]
        public void SemesterIsCurrentOrNext( string today, string start, string end )
        {
            Semester semester = Semester.Containing( DateOnly.Parse( today ) );

            Assert.Equal( DateOnly.Parse( start ), semester.Start );
            Assert.Equal( DateOnly.Parse( end ), semester.End );
        }

        [Fact]
        public void WeeksAlternateFromOddAndSkipSundaysAndHolidays()
        {
            Semester semester = new( new DateOnly( 2026, 9, 1 ), new DateOnly( 2026, 12, 28 ) );
            (DayOfWeek Day, WeekParity Weeks)[] template =
            [
                (DayOfWeek.Monday, WeekParity.Odd),
                (DayOfWeek.Wednesday, WeekParity.Every),
                (DayOfWeek.Sunday, WeekParity.Every),
            ];

            (DateOnly Date, (DayOfWeek Day, WeekParity Weeks) Entry)[] lessons =
                [ .. SemesterPlan.Expand( semester, template, entry => entry.Day, entry => entry.Weeks ) ];

            // 1 сентября 2026 вторник, неделя с ним первая и нечётная. Понедельник 7-го уже во второй, чётной.
            DateOnly[] mondays = [ .. lessons.Where( item => item.Date.DayOfWeek == DayOfWeek.Monday ).Select( item => item.Date ).Take( 3 ) ];
            Assert.Equal( [ new DateOnly( 2026, 9, 14 ), new DateOnly( 2026, 9, 28 ), new DateOnly( 2026, 10, 12 ) ], mondays );
            Assert.Equal( WeekParity.Odd, semester.ParityOf( new DateOnly( 2026, 9, 1 ) ) );
            Assert.Equal( WeekParity.Even, semester.ParityOf( new DateOnly( 2026, 9, 7 ) ) );

            Assert.DoesNotContain( lessons, item => item.Date.DayOfWeek == DayOfWeek.Sunday );
            // 4 ноября 2026 среда, праздник.
            Assert.DoesNotContain( lessons, item => item.Date == new DateOnly( 2026, 11, 4 ) );
            Assert.Contains( lessons, item => item.Date == new DateOnly( 2026, 11, 11 ) );
        }
    }
}
