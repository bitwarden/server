using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class GroupAccessAllDefaultValue : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<bool>(
            name: "AccessAll",
            table: "Group",
            type: "INTEGER",
            nullable: false,
            defaultValue: false,
            oldClrType: typeof(bool),
            oldType: "INTEGER");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<bool>(
            name: "AccessAll",
            table: "Group",
            type: "INTEGER",
            nullable: false,
            oldClrType: typeof(bool),
            oldType: "INTEGER",
            oldDefaultValue: false);
    }
}
