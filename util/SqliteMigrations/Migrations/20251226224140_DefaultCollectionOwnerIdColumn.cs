using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations
{
    /// <inheritdoc />
    public partial class DefaultCollectionOwnerIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultCollectionOwnerId",
                table: "Collection",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collection_DefaultCollectionOwnerId_OrganizationId_Type",
                table: "Collection",
                columns: new[] { "DefaultCollectionOwnerId", "OrganizationId", "Type" },
                unique: true,
                filter: "[Type] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Collection_OrganizationUser_DefaultCollectionOwnerId",
                table: "Collection",
                column: "DefaultCollectionOwnerId",
                principalTable: "OrganizationUser",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Collection_OrganizationUser_DefaultCollectionOwnerId",
                table: "Collection");

            migrationBuilder.DropIndex(
                name: "IX_Collection_DefaultCollectionOwnerId_OrganizationId_Type",
                table: "Collection");

            migrationBuilder.DropColumn(
                name: "DefaultCollectionOwnerId",
                table: "Collection");
        }
    }
}
