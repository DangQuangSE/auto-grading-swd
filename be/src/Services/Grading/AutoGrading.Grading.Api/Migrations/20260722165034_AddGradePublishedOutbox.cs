using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoGrading.Grading.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGradePublishedOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grade_published_outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinalGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinalScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grade_published_outbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_grade_published_outbox_DispatchedAt_CreatedAt",
                table: "grade_published_outbox",
                columns: new[] { "DispatchedAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grade_published_outbox");
        }
    }
}
