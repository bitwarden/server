using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

public partial class LastUserDates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "LastEmailChangeDate",
            table: "User",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastKdfChangeDate",
            table: "User",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastKeyRotationDate",
            table: "User",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastPasswordChangeDate",
            table: "User",
            type: "datetime(6)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastEmailChangeDate",
            table: "User");

        migrationBuilder.DropColumn(
            name: "LastKdfChangeDate",
            table: "User");

        migrationBuilder.DropColumn(
            name: "LastKeyRotationDate",
            table: "User");

        migrationBuilder.DropColumn(
            name: "LastPasswordChangeDate",
            table: "User");
    }
}
