using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoGrading.SubmissionSvc.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionAttemptNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                table: "submissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT Id, ROW_NUMBER() OVER (PARTITION BY AssignmentId, StudentId ORDER BY CreatedAt, Id) AS AttemptNumber
                    FROM submissions
                )
                UPDATE s SET AttemptNumber = n.AttemptNumber
                FROM submissions s INNER JOIN numbered n ON s.Id = n.Id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_submissions_AssignmentId_StudentId_AttemptNumber",
                table: "submissions",
                columns: new[] { "AssignmentId", "StudentId", "AttemptNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_submissions_AssignmentId_StudentId_AttemptNumber",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "AttemptNumber",
                table: "submissions");
        }
    }
}
