using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

public partial class ProviderAddProviderTypeBillingPhone : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BillingPhone",
            table: "Provider",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<byte>(
            name: "Type",
            table: "Provider",
            type: "smallint",
            nullable: false,
            defaultValue: (byte)0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BillingPhone",
            table: "Provider");

        migrationBuilder.DropColumn(
            name: "Type",
            table: "Provider");
    }
}
