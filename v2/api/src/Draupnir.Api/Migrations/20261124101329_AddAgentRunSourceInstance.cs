using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Draupnir.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentRunSourceInstance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentRuns_Source_ExternalTaskId_Attempt",
                table: "AgentRuns");

            migrationBuilder.AddColumn<string>(
                name: "SourceInstance",
                table: "AgentRuns",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Existing rows predate source-instance namespaces. Use the owning project's
            // stable key as the deterministic legacy namespace; orphan runs get "legacy".
            migrationBuilder.Sql("""
                UPDATE "AgentRuns" AS ar
                SET "SourceInstance" = COALESCE(
                    (SELECT p."Key" FROM "Projects" AS p WHERE p."Id" = ar."ProjectId"),
                    'legacy')
                WHERE ar."SourceInstance" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_Source_SourceInstance_ExternalTaskId_Attempt",
                table: "AgentRuns",
                columns: new[] { "Source", "SourceInstance", "ExternalTaskId", "Attempt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentRuns_Source_SourceInstance_ExternalTaskId_Attempt",
                table: "AgentRuns");

            migrationBuilder.DropColumn(
                name: "SourceInstance",
                table: "AgentRuns");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_Source_ExternalTaskId_Attempt",
                table: "AgentRuns",
                columns: new[] { "Source", "ExternalTaskId", "Attempt" },
                unique: true);
        }
    }
}
