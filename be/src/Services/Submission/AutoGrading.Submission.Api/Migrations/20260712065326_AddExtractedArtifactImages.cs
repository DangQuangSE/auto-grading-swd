using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoGrading.SubmissionSvc.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExtractedArtifactImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagesJson",
                table: "extracted_artifacts",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagesJson",
                table: "extracted_artifacts");
        }
    }
}
