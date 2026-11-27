using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Draupnir.Api.Migrations
{
    /// <inheritdoc />
    public partial class SpoolResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Data",
                table: "Evidence",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Evidence",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpoolResultHash",
                table: "Evidence",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_ProjectId_SpoolResultHash",
                table: "Evidence",
                columns: new[] { "ProjectId", "SpoolResultHash" },
                unique: true,
                filter: "\"SpoolResultHash\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Evidence_Projects_ProjectId",
                table: "Evidence",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Evidence_Projects_ProjectId",
                table: "Evidence");

            migrationBuilder.DropIndex(
                name: "IX_Evidence_ProjectId_SpoolResultHash",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "Data",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "SpoolResultHash",
                table: "Evidence");
        }
    }
}
