using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoGrading.Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentSubjectEnrollments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegistrationStatus",
                table: "subjects",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "closed");

            migrationBuilder.AddColumn<Guid>(
                name: "EnrollmentSubjectId",
                table: "classes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "classes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                table: "classes",
                type: "uniqueidentifier",
                nullable: true);

            // Preserve legacy rows without inventing a Subject. EnrollmentSubjectId uses the
            // Class Id as a non-enrollable scope until an admin explicitly maps the Class.
            migrationBuilder.Sql("""
                UPDATE [classes]
                SET [EnrollmentSubjectId] = [Id],
                    [NormalizedName] = UPPER(LTRIM(RTRIM([Name])))
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_classes_Id_EnrollmentSubjectId",
                table: "classes",
                columns: new[] { "Id", "EnrollmentSubjectId" });

            migrationBuilder.CreateTable(
                name: "student_enrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_enrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_student_enrollments_classes_ClassId_SubjectId",
                        columns: x => new { x.ClassId, x.SubjectId },
                        principalTable: "classes",
                        principalColumns: new[] { "Id", "EnrollmentSubjectId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_enrollments_subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_classes_SubjectId",
                table: "classes",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_classes_SubjectId_NormalizedName",
                table: "classes",
                columns: new[] { "SubjectId", "NormalizedName" },
                unique: true,
                filter: "[SubjectId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_classes_EnrollmentSubject",
                table: "classes",
                sql: "([SubjectId] IS NULL AND [EnrollmentSubjectId] = [Id]) OR [EnrollmentSubjectId] = [SubjectId]");

            migrationBuilder.CreateIndex(
                name: "IX_student_enrollments_ClassId",
                table: "student_enrollments",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_student_enrollments_ClassId_SubjectId",
                table: "student_enrollments",
                columns: new[] { "ClassId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_student_enrollments_StudentId",
                table: "student_enrollments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_student_enrollments_StudentId_SubjectId",
                table: "student_enrollments",
                columns: new[] { "StudentId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_enrollments_SubjectId",
                table: "student_enrollments",
                column: "SubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_classes_subjects_SubjectId",
                table: "classes",
                column: "SubjectId",
                principalTable: "subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_classes_subjects_SubjectId",
                table: "classes");

            migrationBuilder.DropTable(
                name: "student_enrollments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_classes_Id_EnrollmentSubjectId",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "IX_classes_SubjectId",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "IX_classes_SubjectId_NormalizedName",
                table: "classes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_classes_EnrollmentSubject",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "RegistrationStatus",
                table: "subjects");

            migrationBuilder.DropColumn(
                name: "EnrollmentSubjectId",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "classes");
        }
    }
}
