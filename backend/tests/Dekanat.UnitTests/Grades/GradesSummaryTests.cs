using Dekanat.Api.Features.Grades;
using Dekanat.Api.Features.Schedule;

namespace Dekanat.UnitTests.Grades
{
    /// <summary>
    /// Юнит-тесты на BuildSummary: проверяем, что средний балл, долги и остаток считаются верно.
    /// </summary>
    public sealed class GradesSummaryTests
    {
        // ── Вспомогательный фабричный метод ──────────────────────────────────

        private static Assessment Make(
            ControlForm form,
            AssessmentStatus status,
            int? grade = null,
            int? points = null,
            DateOnly? date = null,
            bool? isPassed = null )
        {
            return new Assessment
            {
                Id = Guid.NewGuid(),
                StudentProfileId = Guid.NewGuid(),
                DisciplineId = Guid.NewGuid(),
                Discipline = new Discipline { Id = Guid.NewGuid(), Name = "Тест" },
                Semester = 1,
                ControlForm = form,
                Grade = grade,
                IsPassed = isPassed,
                Points = points,
                Date = date,
                Status = status,
            };
        }

        // ── Средний балл ──────────────────────────────────────────────────────

        [Fact]
        public void AverageGrade_IsNull_WhenNoPassedNumericGrades()
        {
            List<Assessment> assessments =
            [
                Make( ControlForm.Credit, AssessmentStatus.Passed, isPassed: true ),
                Make( ControlForm.Exam,   AssessmentStatus.Failed ),
            ];

            GradesSummaryDto summary = GradesEndpoints.BuildSummary( assessments );

            Assert.Null( summary.AverageGrade );
        }

        [Fact]
        public void AverageGrade_ExcludesUnsatisfactory_FromAverage()
        {
            // Оценка 2 не должна входить в средний балл (долг).
            List<Assessment> assessments =
            [
                Make( ControlForm.Exam, AssessmentStatus.Passed, grade: 5, date: DateOnly.FromDateTime( DateTime.Today ) ),
                Make( ControlForm.Exam, AssessmentStatus.Passed, grade: 4, date: DateOnly.FromDateTime( DateTime.Today ) ),
                Make( ControlForm.Exam, AssessmentStatus.Debt,   grade: 2 ),
            ];

            GradesSummaryDto summary = GradesEndpoints.BuildSummary( assessments );

            Assert.Equal( 4.5, summary.AverageGrade );
        }

        [Fact]
        public void AverageGrade_RoundedToTwoDecimals()
        {
            List<Assessment> assessments =
            [
                Make( ControlForm.Exam, AssessmentStatus.Passed, grade: 5, date: DateOnly.FromDateTime( DateTime.Today ) ),
                Make( ControlForm.Exam, AssessmentStatus.Passed, grade: 4, date: DateOnly.FromDateTime( DateTime.Today ) ),
                Make( ControlForm.Exam, AssessmentStatus.Passed, grade: 4, date: DateOnly.FromDateTime( DateTime.Today ) ),
            ];

            GradesSummaryDto summary = GradesEndpoints.BuildSummary( assessments );

            // (5+4+4)/3 = 4.333...  округлено до 4.33
            Assert.Equal( 4.33, summary.AverageGrade );
        }

        [Fact]
        public void AverageGrade_IncludesGradedCredit()
        {
            List<Assessment> assessments =
            [
                Make( ControlForm.Exam,         AssessmentStatus.Passed, grade: 5, date: DateOnly.FromDateTime( DateTime.Today ) ),
                Make( ControlForm.GradedCredit, AssessmentStatus.Passed, grade: 5, date: DateOnly.FromDateTime( DateTime.Today ) ),
            ];

            GradesSummaryDto summary = GradesEndpoints.BuildSummary( assessments );

            Assert.Equal( 5.0, summary.AverageGrade );
        }

        // ── Долги ─────────────────────────────────────────────────────────────

        [Fact]
        public void DebtCount_CountsOnlyDebtStatus()
        {
            List<Assessment> assessments =
            [
                Make( ControlForm.Exam,   AssessmentStatus.Debt ),
                Make( ControlForm.Credit, AssessmentStatus.Debt ),
                Make( ControlForm.Exam,   AssessmentStatus.Passed, grade: 5, date: DateOnly.FromDateTime( DateTime.Today ) ),
                Make( ControlForm.Exam,   AssessmentStatus.Failed ),
            ];

            GradesSummaryDto summary = GradesEndpoints.BuildSummary( assessments );

            Assert.Equal( 2, summary.DebtCount );
        }

        [Fact]
        public void DebtCount_IsZero_WhenNoDebts()
        {
            List<Assessment> assessments =
            [
                Make( ControlForm.Exam,   AssessmentStatus.Passed, grade: 5, date: DateOnly.FromDateTime( DateTime.Today ) ),
                Make( ControlForm.Credit, AssessmentStatus.Passed, isPassed: true ),
            ];

            GradesSummaryDto summary = GradesEndpoints.BuildSummary( assessments );

            Assert.Equal( 0, summary.DebtCount );
        }

        // ── Осталось сдать ────────────────────────────────────────────────────

        [Fact]
        public void RemainingCount_IncludesFailed_NotAdmitted_AndUndated()
        {
            List<Assessment> assessments =
            [
                Make( ControlForm.Exam,   AssessmentStatus.Failed ),
                Make( ControlForm.Exam,   AssessmentStatus.NotAdmitted ),
                Make( ControlForm.Credit, AssessmentStatus.Failed ),   // без даты
                Make( ControlForm.Exam,   AssessmentStatus.Passed, grade: 5, date: DateOnly.FromDateTime( DateTime.Today ) ),
                Make( ControlForm.Exam,   AssessmentStatus.Debt ),     // долг — не в «остатке»
            ];

            GradesSummaryDto summary = GradesEndpoints.BuildSummary( assessments );

            Assert.Equal( 3, summary.RemainingCount );
        }

        [Fact]
        public void RemainingCount_IsZero_WhenAllPassed()
        {
            List<Assessment> assessments =
            [
                Make( ControlForm.Exam,   AssessmentStatus.Passed, grade: 4, date: DateOnly.FromDateTime( DateTime.Today ) ),
                Make( ControlForm.Credit, AssessmentStatus.Passed, isPassed: true, date: DateOnly.FromDateTime( DateTime.Today ) ),
            ];

            GradesSummaryDto summary = GradesEndpoints.BuildSummary( assessments );

            Assert.Equal( 0, summary.RemainingCount );
        }

        // ── Пустой список ─────────────────────────────────────────────────────

        [Fact]
        public void EmptyList_ReturnsZerosAndNullAverage()
        {
            GradesSummaryDto summary = GradesEndpoints.BuildSummary( [] );

            Assert.Null( summary.AverageGrade );
            Assert.Equal( 0, summary.DebtCount );
            Assert.Equal( 0, summary.RemainingCount );
        }

        // ── Совпадение сводки с ручным подсчётом (сценарий «реального» студента) ──

        [Fact]
        public void Summary_MatchesManualCalculation_ForTypicalStudent()
        {
            // Студент с 4 дисциплинами: 2 сданы, 1 долг, 1 не сдана.
            List<Assessment> assessments =
            [
                Make( ControlForm.Exam,   AssessmentStatus.Passed,  grade: 5, date: new DateOnly( 2026, 1, 15 ) ),
                Make( ControlForm.Exam,   AssessmentStatus.Passed,  grade: 4, date: new DateOnly( 2026, 1, 18 ) ),
                Make( ControlForm.Credit, AssessmentStatus.Debt ),
                Make( ControlForm.Exam,   AssessmentStatus.Failed ),
            ];

            GradesSummaryDto summary = GradesEndpoints.BuildSummary( assessments );

            Assert.Equal( 4.5, summary.AverageGrade );  // (5+4)/2
            Assert.Equal( 1, summary.DebtCount );
            Assert.Equal( 1, summary.RemainingCount );  // только Failed
        }
    }
}
