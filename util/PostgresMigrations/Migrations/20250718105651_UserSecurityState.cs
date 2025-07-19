using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class UserSecurityState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SecurityState",
            table: "User",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "SecurityVersion",
            table: "User",
            type: "integer",
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
