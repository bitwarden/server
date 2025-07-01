using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddOrgUserDefaultCollection : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DefaultUserCollectionEmail",
            table: "Collection",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Type",
            table: "Collection",
            type: "integer",
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
