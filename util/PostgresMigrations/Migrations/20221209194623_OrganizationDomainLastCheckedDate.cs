using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations
{
    public partial class OrganizationDomainLastCheckedDate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastCheckedDate",
                table: "OrganizationDomain",
                type: "timestamp without time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastCheckedDate",
                table: "OrganizationDomain");
        }
    }
}
