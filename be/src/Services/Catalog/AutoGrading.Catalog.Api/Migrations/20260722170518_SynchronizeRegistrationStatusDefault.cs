using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoGrading.Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class SynchronizeRegistrationStatusDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RegistrationStatus",
                table: "subjects",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValueSql: "'closed'",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "closed");

            migrationBuilder.Sql("""
                IF COL_LENGTH('rubric_criteria', 'Code') IS NULL
                    ALTER TABLE [rubric_criteria] ADD [Code] nvarchar(max) NOT NULL CONSTRAINT [DF_rubric_criteria_Code] DEFAULT N'';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('rubric_criteria', 'Code') IS NOT NULL
                BEGIN
                    DECLARE @codeDefault sysname;
                    SELECT @codeDefault = dc.name FROM sys.default_constraints dc
                    JOIN sys.columns c ON c.default_object_id = dc.object_id
                    WHERE dc.parent_object_id = OBJECT_ID('rubric_criteria') AND c.name = 'Code';
                    IF @codeDefault IS NOT NULL EXEC('ALTER TABLE [rubric_criteria] DROP CONSTRAINT [' + @codeDefault + ']');
                    ALTER TABLE [rubric_criteria] DROP COLUMN [Code];
                END
                """);

            migrationBuilder.AlterColumn<string>(
                name: "RegistrationStatus",
                table: "subjects",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "closed",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValueSql: "'closed'");
        }
    }
}
