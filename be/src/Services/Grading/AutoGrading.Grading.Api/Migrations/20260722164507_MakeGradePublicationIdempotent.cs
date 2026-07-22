using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoGrading.Grading.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeGradePublicationIdempotent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH duplicates AS (
                    SELECT Id, ROW_NUMBER() OVER (PARTITION BY SubmissionId ORDER BY PublishedAt DESC, Id DESC) AS row_number
                    FROM grade_publications
                )
                DELETE FROM grade_publications WHERE Id IN (SELECT Id FROM duplicates WHERE row_number > 1);
                """);
            migrationBuilder.CreateIndex(
                name: "IX_grade_publications_FinalGradeId",
                table: "grade_publications",
                column: "FinalGradeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grade_publications_SubmissionId",
                table: "grade_publications",
                column: "SubmissionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_grade_publications_FinalGradeId",
                table: "grade_publications");

            migrationBuilder.DropIndex(
                name: "IX_grade_publications_SubmissionId",
                table: "grade_publications");
        }
    }
}
