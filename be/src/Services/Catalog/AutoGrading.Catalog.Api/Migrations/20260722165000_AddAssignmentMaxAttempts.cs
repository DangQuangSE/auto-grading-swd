using AutoGrading.Catalog.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoGrading.Catalog.Api.Migrations;

[DbContext(typeof(CatalogDbContext))]
[Migration("20260722165000_AddAssignmentMaxAttempts")]
public partial class AddAssignmentMaxAttempts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<int>(name: "MaxAttempts", table: "assignments", type: "int", nullable: false, defaultValue: 1);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "MaxAttempts", table: "assignments");
}

