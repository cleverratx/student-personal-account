using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dekanat.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGrades : Migration
    {
        /// <inheritdoc />
        protected override void Up( MigrationBuilder migrationBuilder )
        {
            // ── assessments ──────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "assessments",
                columns: table => new
                {
                    id = table.Column<Guid>( type: "uuid", nullable: false ),
                    student_profile_id = table.Column<Guid>( type: "uuid", nullable: false ),
                    discipline_id = table.Column<Guid>( type: "uuid", nullable: false ),
                    semester = table.Column<int>( type: "integer", nullable: false ),
                    control_form = table.Column<string>( type: "character varying(16)", maxLength: 16, nullable: false ),
                    grade = table.Column<int>( type: "integer", nullable: true ),
                    is_passed = table.Column<bool>( type: "boolean", nullable: true ),
                    points = table.Column<int>( type: "integer", nullable: true ),
                    date = table.Column<DateOnly>( type: "date", nullable: true ),
                    status = table.Column<string>( type: "character varying(16)", maxLength: 16, nullable: false ),
                },
                constraints: table =>
                {
                    table.PrimaryKey( "pk_assessments", x => x.id );
                    table.CheckConstraint( "ck_assessments_control_form",
                        "control_form IN ('Exam', 'Credit', 'GradedCredit', 'CourseWork')" );
                    table.CheckConstraint( "ck_assessments_status",
                        "status IN ('Passed', 'Failed', 'NotAdmitted', 'Debt')" );
                    table.CheckConstraint( "ck_assessments_semester_positive", "semester > 0" );
                    table.CheckConstraint( "ck_assessments_grade_range", "grade IS NULL OR (grade >= 2 AND grade <= 5)" );
                    table.CheckConstraint( "ck_assessments_points_range", "points IS NULL OR (points >= 0 AND points <= 100)" );
                    table.ForeignKey(
                        name: "fk_assessments_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade );
                    table.ForeignKey(
                        name: "fk_assessments_disciplines_discipline_id",
                        column: x => x.discipline_id,
                        principalTable: "disciplines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict );
                } );

            migrationBuilder.CreateIndex(
                name: "ix_assessments_student_profile_id_discipline_id_semester_control_form",
                table: "assessments",
                columns: [ "student_profile_id", "discipline_id", "semester", "control_form" ],
                unique: true );

            migrationBuilder.CreateIndex(
                name: "ix_assessments_student_profile_id_semester",
                table: "assessments",
                columns: [ "student_profile_id", "semester" ] );

            // ── session_events ───────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "session_events",
                columns: table => new
                {
                    id = table.Column<Guid>( type: "uuid", nullable: false ),
                    group_id = table.Column<Guid>( type: "uuid", nullable: false ),
                    discipline_id = table.Column<Guid>( type: "uuid", nullable: false ),
                    teacher_id = table.Column<Guid>( type: "uuid", nullable: false ),
                    type = table.Column<string>( type: "character varying(16)", maxLength: 16, nullable: false ),
                    starts_at = table.Column<DateTime>( type: "timestamp with time zone", nullable: false ),
                    ends_at = table.Column<DateTime>( type: "timestamp with time zone", nullable: false ),
                    room = table.Column<string>( type: "character varying(32)", maxLength: 32, nullable: false ),
                    semester = table.Column<int>( type: "integer", nullable: false ),
                },
                constraints: table =>
                {
                    table.PrimaryKey( "pk_session_events", x => x.id );
                    table.CheckConstraint( "ck_session_events_type",
                        "type IN ('Exam', 'Credit', 'GradedCredit', 'Consultation')" );
                    table.CheckConstraint( "ck_session_events_ends_after_starts", "ends_at > starts_at" );
                    table.CheckConstraint( "ck_session_events_semester_positive", "semester > 0" );
                    table.ForeignKey(
                        name: "fk_session_events_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "study_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict );
                    table.ForeignKey(
                        name: "fk_session_events_disciplines_discipline_id",
                        column: x => x.discipline_id,
                        principalTable: "disciplines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict );
                    table.ForeignKey(
                        name: "fk_session_events_teachers_teacher_id",
                        column: x => x.teacher_id,
                        principalTable: "teachers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict );
                } );

            migrationBuilder.CreateIndex(
                name: "ix_session_events_group_id_semester_starts_at",
                table: "session_events",
                columns: [ "group_id", "semester", "starts_at" ] );
        }

        /// <inheritdoc />
        protected override void Down( MigrationBuilder migrationBuilder )
        {
            migrationBuilder.DropTable( name: "session_events" );
            migrationBuilder.DropTable( name: "assessments" );
        }
    }
}
