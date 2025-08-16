using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McpManager.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNamespaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "McpNamespaceId",
                table: "McpToolRequests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    McpConnectionTimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    McpRetryAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    AllowNewUserSignup = table.Column<bool>(type: "INTEGER", nullable: false),
                    SessionLifetimeDays = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "McpNamespaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    RateLimitEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    RateLimitRequestsPerMinute = table.Column<int>(type: "INTEGER", nullable: false),
                    RateLimitStrategy = table.Column<int>(type: "INTEGER", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpNamespaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeyNamespaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    McpNamespaceId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeyNamespaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKeyNamespaces_ApiKeys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "ApiKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApiKeyNamespaces_McpNamespaces_McpNamespaceId",
                        column: x => x.McpNamespaceId,
                        principalTable: "McpNamespaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "McpNamespaceServers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    McpNamespaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    McpServerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpNamespaceServers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpNamespaceServers_McpNamespaces_McpNamespaceId",
                        column: x => x.McpNamespaceId,
                        principalTable: "McpNamespaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_McpNamespaceServers_McpServers_McpServerId",
                        column: x => x.McpServerId,
                        principalTable: "McpServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "McpNamespaceTools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    NameOverride = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    DescriptionOverride = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    McpNamespaceServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    McpToolId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpNamespaceTools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpNamespaceTools_McpNamespaceServers_McpNamespaceServerId",
                        column: x => x.McpNamespaceServerId,
                        principalTable: "McpNamespaceServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_McpNamespaceTools_McpTools_McpToolId",
                        column: x => x.McpToolId,
                        principalTable: "McpTools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Id", "AllowNewUserSignup", "McpConnectionTimeoutSeconds", "McpRetryAttempts", "SessionLifetimeDays" },
                values: new object[] { 1, true, 30, 3, 7 });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000000"),
                column: "PasswordHash",
                value: "AQAAAAIAAYagAAAAEOqOiuA0bHdat2FXMTttZDTVksgYKQ9t5Pzc92oGjnNRcGUSiPUQOM00wFjo1eeblQ==");

            migrationBuilder.CreateIndex(
                name: "IX_McpToolRequests_McpNamespaceId",
                table: "McpToolRequests",
                column: "McpNamespaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyNamespaces_ApiKeyId_McpNamespaceId",
                table: "ApiKeyNamespaces",
                columns: new[] { "ApiKeyId", "McpNamespaceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyNamespaces_McpNamespaceId",
                table: "ApiKeyNamespaces",
                column: "McpNamespaceId");

            migrationBuilder.CreateIndex(
                name: "IX_McpNamespaces_Slug",
                table: "McpNamespaces",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpNamespaceServers_McpNamespaceId_McpServerId",
                table: "McpNamespaceServers",
                columns: new[] { "McpNamespaceId", "McpServerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpNamespaceServers_McpServerId",
                table: "McpNamespaceServers",
                column: "McpServerId");

            migrationBuilder.CreateIndex(
                name: "IX_McpNamespaceTools_McpNamespaceServerId_McpToolId",
                table: "McpNamespaceTools",
                columns: new[] { "McpNamespaceServerId", "McpToolId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpNamespaceTools_McpToolId",
                table: "McpNamespaceTools",
                column: "McpToolId");

            migrationBuilder.AddForeignKey(
                name: "FK_McpToolRequests_McpNamespaces_McpNamespaceId",
                table: "McpToolRequests",
                column: "McpNamespaceId",
                principalTable: "McpNamespaces",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_McpToolRequests_McpNamespaces_McpNamespaceId",
                table: "McpToolRequests");

            migrationBuilder.DropTable(
                name: "ApiKeyNamespaces");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "McpNamespaceTools");

            migrationBuilder.DropTable(
                name: "McpNamespaceServers");

            migrationBuilder.DropTable(
                name: "McpNamespaces");

            migrationBuilder.DropIndex(
                name: "IX_McpToolRequests_McpNamespaceId",
                table: "McpToolRequests");

            migrationBuilder.DropColumn(
                name: "McpNamespaceId",
                table: "McpToolRequests");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000000"),
                column: "PasswordHash",
                value: "AQAAAAEAACcQAAAAEM0hozIbn2LDkSW9sK1Mkl6OSdNaqQmY4B76PqGY97iuUep8RG5+zPjIWpiq8o0XVg==");
        }
    }
}
