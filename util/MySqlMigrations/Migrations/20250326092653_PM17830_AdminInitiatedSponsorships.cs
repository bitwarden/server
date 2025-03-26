using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class PM17830_AdminInitiatedSponsorships : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsAdminInitiated",
            table: "OrganizationSponsorship",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "Notes",
            table: "OrganizationSponsorship",
            type: "longtext",
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<bool>(
            name: "UseAdminSponsoredFamilies",
            table: "Organization",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsAdminInitiated",
            table: "OrganizationSponsorship");

        migrationBuilder.DropColumn(
            name: "Notes",
            table: "OrganizationSponsorship");

        migrationBuilder.DropColumn(
            name: "UseAdminSponsoredFamilies",
            table: "Organization");
    }
}
