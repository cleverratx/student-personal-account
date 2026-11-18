using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dekanat.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "disciplines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_disciplines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "teachers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    position = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teachers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lessons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    room = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lessons", x => x.id);
                    table.CheckConstraint("ck_lessons_ends_after_starts", "ends_at > starts_at");
                    table.CheckConstraint("ck_lessons_type", "type IN ('Lecture', 'Practice', 'Lab', 'Consultation', 'Exam', 'Credit', 'GradedCredit')");
                    table.ForeignKey(
                        name: "fk_lessons_disciplines_discipline_id",
                        column: x => x.discipline_id,
                        principalTable: "disciplines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lessons_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lessons_teachers_teacher_id",
                        column: x => x.teacher_id,
                        principalTable: "teachers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lesson_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    old_starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    new_starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    old_room = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    new_room = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lesson_changes", x => x.id);
                    table.CheckConstraint("ck_lesson_changes_type", "type IN ('Reschedule', 'RoomChange', 'Cancellation')");
                    table.ForeignKey(
                        name: "fk_lesson_changes_lessons_lesson_id",
                        column: x => x.lesson_id,
                        principalTable: "lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lesson_changes_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_disciplines_name",
                table: "disciplines",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lesson_changes_changed_by_user_id",
                table: "lesson_changes",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_lesson_changes_lesson_id_changed_at",
                table: "lesson_changes",
                columns: new[] { "lesson_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_lessons_discipline_id",
                table: "lessons",
                column: "discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_lessons_group_id_starts_at",
                table: "lessons",
                columns: new[] { "group_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_lessons_room_starts_at",
                table: "lessons",
                columns: new[] { "room", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_lessons_teacher_id_starts_at",
                table: "lessons",
                columns: new[] { "teacher_id", "starts_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lesson_changes");

            migrationBuilder.DropTable(
                name: "lessons");

            migrationBuilder.DropTable(
                name: "disciplines");

            migrationBuilder.DropTable(
                name: "teachers");
        }
    }
}
