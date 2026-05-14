using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McpManager.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedAppSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AllowNewUserSignup", table: "AppSettings");

            migrationBuilder.DropColumn(name: "SessionLifetimeDays", table: "AppSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowNewUserSignup",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<int>(
                name: "SessionLifetimeDays",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AllowNewUserSignup", "SessionLifetimeDays" },
                values: new object[] { true, 7 }
            );
        }
    }
}
