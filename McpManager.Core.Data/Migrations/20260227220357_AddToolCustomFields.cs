using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McpManager.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddToolCustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomDescription",
                table: "McpTools",
                type: "TEXT",
                maxLength: 2000,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "CustomInputSchema",
                table: "McpTools",
                type: "TEXT",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CustomDescription", table: "McpTools");

            migrationBuilder.DropColumn(name: "CustomInputSchema", table: "McpTools");
        }
    }
}
