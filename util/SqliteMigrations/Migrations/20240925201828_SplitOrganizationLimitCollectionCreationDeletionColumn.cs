using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class SplitOrganizationLimitCollectionCreationDeletionColumn : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<bool>(
            name: "LimitCollectionCreationDeletion",
            table: "Organization",
            type: "INTEGER",
            nullable: false,
            oldClrType: typeof(bool),
            oldType: "INTEGER",
            oldDefaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "LimitCollectionCreation",
            table: "Organization",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "LimitCollectionDeletion",
            table: "Organization",
            type: "INTEGER",
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
            type: "INTEGER",
            nullable: false,
            defaultValue: true,
            oldClrType: typeof(bool),
            oldType: "INTEGER");
    }
}
