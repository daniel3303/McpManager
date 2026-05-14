using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McpManager.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveApiKeyNamespaceEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiKeyNamespaces_ApiKeys_ApiKeyId",
                table: "ApiKeyNamespaces"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_ApiKeyNamespaces_McpNamespaces_McpNamespaceId",
                table: "ApiKeyNamespaces"
            );

            migrationBuilder.DropPrimaryKey(name: "PK_ApiKeyNamespaces", table: "ApiKeyNamespaces");

            migrationBuilder.DropIndex(
                name: "IX_ApiKeyNamespaces_ApiKeyId_McpNamespaceId",
                table: "ApiKeyNamespaces"
            );

            migrationBuilder.DropColumn(name: "Id", table: "ApiKeyNamespaces");

            migrationBuilder.RenameColumn(
                name: "McpNamespaceId",
                table: "ApiKeyNamespaces",
                newName: "ApiKeysId"
            );

            migrationBuilder.RenameColumn(
                name: "ApiKeyId",
                table: "ApiKeyNamespaces",
                newName: "AllowedNamespacesId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_ApiKeyNamespaces_McpNamespaceId",
                table: "ApiKeyNamespaces",
                newName: "IX_ApiKeyNamespaces_ApiKeysId"
            );

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApiKeyNamespaces",
                table: "ApiKeyNamespaces",
                columns: new[] { "AllowedNamespacesId", "ApiKeysId" }
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeyNamespaces_ApiKeys_ApiKeysId",
                table: "ApiKeyNamespaces",
                column: "ApiKeysId",
                principalTable: "ApiKeys",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeyNamespaces_McpNamespaces_AllowedNamespacesId",
                table: "ApiKeyNamespaces",
                column: "AllowedNamespacesId",
                principalTable: "McpNamespaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiKeyNamespaces_ApiKeys_ApiKeysId",
                table: "ApiKeyNamespaces"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_ApiKeyNamespaces_McpNamespaces_AllowedNamespacesId",
                table: "ApiKeyNamespaces"
            );

            migrationBuilder.DropPrimaryKey(name: "PK_ApiKeyNamespaces", table: "ApiKeyNamespaces");

            migrationBuilder.RenameColumn(
                name: "ApiKeysId",
                table: "ApiKeyNamespaces",
                newName: "McpNamespaceId"
            );

            migrationBuilder.RenameColumn(
                name: "AllowedNamespacesId",
                table: "ApiKeyNamespaces",
                newName: "ApiKeyId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_ApiKeyNamespaces_ApiKeysId",
                table: "ApiKeyNamespaces",
                newName: "IX_ApiKeyNamespaces_McpNamespaceId"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "ApiKeyNamespaces",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApiKeyNamespaces",
                table: "ApiKeyNamespaces",
                column: "Id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyNamespaces_ApiKeyId_McpNamespaceId",
                table: "ApiKeyNamespaces",
                columns: new[] { "ApiKeyId", "McpNamespaceId" },
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeyNamespaces_ApiKeys_ApiKeyId",
                table: "ApiKeyNamespaces",
                column: "ApiKeyId",
                principalTable: "ApiKeys",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeyNamespaces_McpNamespaces_McpNamespaceId",
                table: "ApiKeyNamespaces",
                column: "McpNamespaceId",
                principalTable: "McpNamespaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }
    }
}
