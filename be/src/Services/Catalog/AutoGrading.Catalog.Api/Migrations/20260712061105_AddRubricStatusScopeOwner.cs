using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoGrading.Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRubricStatusScopeOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing Rubric/RubricCriterion rows are demo data only (no LecturerId, never
            // confirmed through the new flow) — clear them instead of backfilling so the new
            // non-nullable Status/Scope columns start from a clean, consistent state.
            // rubric_criteria has a cascading FK to rubrics, but is cleared explicitly for clarity.
            migrationBuilder.Sql("DELETE FROM rubric_criteria;");
            migrationBuilder.Sql("DELETE FROM rubrics;");

            migrationBuilder.AddColumn<Guid>(
                name: "LecturerId",
                table: "rubrics",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "rubrics",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "rubrics",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Lecturer");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "rubrics",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Parsing");

            migrationBuilder.CreateIndex(
                name: "IX_rubrics_Scope",
                table: "rubrics",
                column: "Scope");

            migrationBuilder.CreateIndex(
                name: "IX_rubrics_Status",
                table: "rubrics",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rubrics_Scope",
                table: "rubrics");

            migrationBuilder.DropIndex(
                name: "IX_rubrics_Status",
                table: "rubrics");

            migrationBuilder.DropColumn(
                name: "LecturerId",
                table: "rubrics");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "rubrics");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "rubrics");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "rubrics");
        }
    }
}
