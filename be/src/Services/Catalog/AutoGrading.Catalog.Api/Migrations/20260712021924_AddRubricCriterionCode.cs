using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoGrading.Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRubricCriterionCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "rubric_criteria",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Code",
                table: "rubric_criteria");
        }
    }
}
