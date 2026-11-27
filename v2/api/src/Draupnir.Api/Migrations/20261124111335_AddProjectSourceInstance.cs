using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Draupnir.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSourceInstance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceInstance",
                table: "Projects",
                type: "text",
                nullable: true);
            // 既有專案在 migration 的當下以 Key 固定來源命名空間；之後改名不會再改變它。
            // 這與 AddAgentRunSourceInstance 的 backfill（當時的 Key）一致。
            migrationBuilder.Sql("UPDATE \"Projects\" SET \"SourceInstance\" = \"Key\" WHERE \"SourceInstance\" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceInstance",
                table: "Projects");
        }
    }
}
