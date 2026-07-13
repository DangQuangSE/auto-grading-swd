using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoGrading.Identity.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityAuthorizationCaches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClassLecturerCaches",
                columns: table => new
                {
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LecturerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassLecturerCaches", x => x.ClassId);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionGraders",
                columns: table => new
                {
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LecturerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionGraders", x => new { x.SubmissionId, x.LecturerId });
                });

            migrationBuilder.CreateTable(
                name: "SubmissionStudents",
                columns: table => new
                {
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionStudents", x => x.SubmissionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionStudents_StudentId",
                table: "SubmissionStudents",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassLecturerCaches");

            migrationBuilder.DropTable(
                name: "SubmissionGraders");

            migrationBuilder.DropTable(
                name: "SubmissionStudents");
        }
    }
}
