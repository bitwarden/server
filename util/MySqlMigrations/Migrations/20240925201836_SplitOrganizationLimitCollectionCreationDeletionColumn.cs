using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class SplitOrganizationLimitCollectionCreationDeletionColumn : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<bool>(
            name: "LimitCollectionCreationDeletion",
            table: "Organization",
            type: "tinyint(1)",
            nullable: false,
            oldClrType: typeof(bool),
            oldType: "tinyint(1)",
            oldDefaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "LimitCollectionCreation",
            table: "Organization",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "LimitCollectionDeletion",
            table: "Organization",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LimitCollectionCreation",
            table: "Organization");

        migrationBuilder.DropColumn(
            name: "LimitCollectionDeletion",
            table: "Organization");

        migrationBuilder.AlterColumn<bool>(
            name: "LimitCollectionCreationDeletion",
            table: "Organization",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: true,
            oldClrType: typeof(bool),
            oldType: "tinyint(1)");
    }
}
