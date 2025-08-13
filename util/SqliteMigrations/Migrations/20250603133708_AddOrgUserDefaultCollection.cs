using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddOrgUserDefaultCollection : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DefaultUserCollectionEmail",
            table: "Collection",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Type",
            table: "Collection",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DefaultUserCollectionEmail",
            table: "Collection");

        migrationBuilder.DropColumn(
            name: "Type",
            table: "Collection");
    }
}
