using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations
{
    public partial class TdeAdminApproval : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "AuthRequest",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_AuthRequest_OrganizationId",
                table: "AuthRequest",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuthRequest_Organization_OrganizationId",
                table: "AuthRequest",
                column: "OrganizationId",
                principalTable: "Organization",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuthRequest_Organization_OrganizationId",
                table: "AuthRequest");

            migrationBuilder.DropIndex(
                name: "IX_AuthRequest_OrganizationId",
                table: "AuthRequest");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "AuthRequest");
        }
    }
}
