using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoGrading.NotificationSvc.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationEventDeduplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IntegrationEventId",
                table: "audit_events",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_IntegrationEventId",
                table: "audit_events",
                column: "IntegrationEventId",
                unique: true,
                filter: "[IntegrationEventId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_events_IntegrationEventId",
                table: "audit_events");

            migrationBuilder.DropColumn(
                name: "IntegrationEventId",
                table: "audit_events");
        }
    }
}
