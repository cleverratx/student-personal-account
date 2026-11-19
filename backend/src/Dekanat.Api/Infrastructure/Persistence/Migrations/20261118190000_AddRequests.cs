using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Dekanat.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "request_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    processing_days = table.Column<int>(type: "integer", nullable: false),
                    field_schema = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_types", x => x.id);
                    table.CheckConstraint("ck_request_types_processing_days", "processing_days > 0");
                });

            migrationBuilder.CreateTable(
                name: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fields = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    current_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    estimated_ready_date = table.Column<DateOnly>(type: "date", nullable: true),
                    issue_place = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    issue_deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requests", x => x.id);
                    table.CheckConstraint("ck_requests_current_status", "current_status IN ('Submitted', 'InProgress', 'Ready', 'Issued', 'Rejected', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_requests_request_types_request_type_id",
                        column: x => x.request_type_id,
                        principalTable: "request_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_requests_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "request_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_status_history", x => x.id);
                    table.CheckConstraint("ck_request_status_history_status", "status IN ('Submitted', 'InProgress', 'Ready', 'Issued', 'Rejected', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_request_status_history_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_request_status_history_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "request_types",
                columns: new[] { "id", "description", "field_schema", "name", "processing_days" },
                values: new object[,]
                {
                    { new Guid("d7a1c3e5-4b2f-4a6d-9c8e-0f1a2b3c4d01"), "Подтверждает, что вы учитесь в вузе", "{\"destination\":{\"type\":\"string\",\"required\":true,\"title\":\"Куда предоставить\",\"maxLength\":200},\"copies\":{\"type\":\"integer\",\"required\":true,\"title\":\"Количество экземпляров\",\"min\":1,\"max\":5},\"delivery\":{\"type\":\"choice\",\"required\":true,\"title\":\"Как получить\",\"options\":[{\"value\":\"Electronic\",\"title\":\"Электронно\"},{\"value\":\"Deanery\",\"title\":\"В деканате\"}]},\"comment\":{\"type\":\"string\",\"required\":false,\"title\":\"Комментарий\",\"maxLength\":500}}", "Справка об обучении", 3 },
                    { new Guid("d7a1c3e5-4b2f-4a6d-9c8e-0f1a2b3c4d02"), "Справка для военного комиссариата", "{\"destination\":{\"type\":\"string\",\"required\":true,\"title\":\"В какой военкомат\",\"maxLength\":200},\"copies\":{\"type\":\"integer\",\"required\":true,\"title\":\"Количество экземпляров\",\"min\":1,\"max\":5},\"delivery\":{\"type\":\"choice\",\"required\":true,\"title\":\"Как получить\",\"options\":[{\"value\":\"Electronic\",\"title\":\"Электронно\"},{\"value\":\"Deanery\",\"title\":\"В деканате\"}]},\"comment\":{\"type\":\"string\",\"required\":false,\"title\":\"Комментарий\",\"maxLength\":500}}", "Справка в военкомат", 5 },
                    { new Guid("d7a1c3e5-4b2f-4a6d-9c8e-0f1a2b3c4d03"), "Для органов соцзащиты или для назначения стипендии", "{\"destination\":{\"type\":\"string\",\"required\":true,\"title\":\"Куда предоставить\",\"maxLength\":200},\"copies\":{\"type\":\"integer\",\"required\":true,\"title\":\"Количество экземпляров\",\"min\":1,\"max\":5},\"delivery\":{\"type\":\"choice\",\"required\":true,\"title\":\"Как получить\",\"options\":[{\"value\":\"Electronic\",\"title\":\"Электронно\"},{\"value\":\"Deanery\",\"title\":\"В деканате\"}]},\"comment\":{\"type\":\"string\",\"required\":false,\"title\":\"Комментарий\",\"maxLength\":500}}", "Справка для соцзащиты или стипендии", 5 },
                    { new Guid("d7a1c3e5-4b2f-4a6d-9c8e-0f1a2b3c4d04"), "Заявление на пересдачу дисциплины", "{\"assessmentId\":{\"type\":\"uuid\",\"required\":true,\"title\":\"Дисциплина\"},\"comment\":{\"type\":\"string\",\"required\":false,\"title\":\"Комментарий\",\"maxLength\":500}}", "Заявление на пересдачу", 2 },
                    { new Guid("d7a1c3e5-4b2f-4a6d-9c8e-0f1a2b3c4d05"), "Заявление на академический отпуск", "{\"reason\":{\"type\":\"choice\",\"required\":true,\"title\":\"Причина\",\"options\":[{\"value\":\"Health\",\"title\":\"По состоянию здоровья\"},{\"value\":\"Family\",\"title\":\"По семейным обстоятельствам\"},{\"value\":\"Other\",\"title\":\"Другая причина\"}]},\"startDate\":{\"type\":\"date\",\"required\":true,\"title\":\"С какой даты\"},\"durationMonths\":{\"type\":\"integer\",\"required\":true,\"title\":\"На сколько месяцев\",\"min\":1,\"max\":12},\"comment\":{\"type\":\"string\",\"required\":false,\"title\":\"Комментарий\",\"maxLength\":500}}", "Заявление на академический отпуск", 10 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_request_status_history_changed_by_user_id",
                table: "request_status_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_status_history_request_id_changed_at",
                table: "request_status_history",
                columns: new[] { "request_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_request_types_name",
                table: "request_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_requests_current_status_created_at",
                table: "requests",
                columns: new[] { "current_status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_requests_request_type_id",
                table: "requests",
                column: "request_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_requests_student_profile_id_created_at",
                table: "requests",
                columns: new[] { "student_profile_id", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "request_status_history");

            migrationBuilder.DropTable(
                name: "requests");

            migrationBuilder.DropTable(
                name: "request_types");
        }
    }
}
