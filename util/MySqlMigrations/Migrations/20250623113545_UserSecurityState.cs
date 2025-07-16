using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class UserSecurityState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SecurityState",
            table: "User",
            type: "varchar(1000)",
            maxLength: 1000,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<int>(
            name: "SecurityVersion",
            table: "User",
            type: "int",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SecurityState",
            table: "User");

        migrationBuilder.DropColumn(
            name: "SecurityVersion",
            table: "User");
    }
}
