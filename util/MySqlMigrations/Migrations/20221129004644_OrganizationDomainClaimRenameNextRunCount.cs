using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations
{
    public partial class OrganizationDomainClaimRenameNextRunCount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NextRunCount",
                table: "OrganizationDomain",
                newName: "JobRunCount");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "JobRunCount",
                table: "OrganizationDomain",
                newName: "NextRunCount");
        }
    }
}
