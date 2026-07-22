using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoGrading.Catalog.Api.Migrations;

public partial class AddAssignmentMaxAttempts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<int>(name: "MaxAttempts", table: "assignments", type: "int", nullable: false, defaultValue: 1);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "MaxAttempts", table: "assignments");
}
