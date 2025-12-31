using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations
{
    /// <inheritdoc />
    public partial class DefaultCollectionSemaphore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DefaultCollectionSemaphore",
                columns: table => new
                {
                    OrganizationUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefaultCollectionSemaphore", x => x.OrganizationUserId);
                    table.ForeignKey(
                        name: "FK_DefaultCollectionSemaphore_OrganizationUser_OrganizationUserId",
                        column: x => x.OrganizationUserId,
                        principalTable: "OrganizationUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DefaultCollectionSemaphore");
        }
    }
}
