using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McpManager.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDefaultMcpConnectionTimeout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "McpConnectionTimeoutSeconds",
                value: 120);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "McpConnectionTimeoutSeconds",
                value: 30);
        }
    }
}
