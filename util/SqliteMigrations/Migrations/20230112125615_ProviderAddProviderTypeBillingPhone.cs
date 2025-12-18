using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

public partial class ProviderAddProviderTypeBillingPhone : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BillingPhone",
            table: "Provider",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<byte>(
            name: "Type",
            table: "Provider",
            type: "INTEGER",
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
