using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.PostgresMigrations.Migrations
{
    public partial class AddCloudBillingSyncKeyToOrganization : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CloudBillingSyncKey",
                table: "Organization",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CloudBillingSyncKey",
                table: "Organization");
        }
    }
}
