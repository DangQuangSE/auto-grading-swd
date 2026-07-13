using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoGrading.Grading.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalRubricCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "local_rubrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RubricId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Scope = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_local_rubrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "local_rubric_criteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalRubricId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RubricCriterionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_local_rubric_criteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_local_rubric_criteria_local_rubrics_LocalRubricId",
                        column: x => x.LocalRubricId,
                        principalTable: "local_rubrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_local_rubric_criteria_LocalRubricId",
                table: "local_rubric_criteria",
                column: "LocalRubricId");

            migrationBuilder.CreateIndex(
                name: "IX_local_rubrics_RubricId",
                table: "local_rubrics",
                column: "RubricId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "local_rubric_criteria");

            migrationBuilder.DropTable(
                name: "local_rubrics");
        }
    }
}
