using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddPermissionUseViewPolicies : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "UseViewPolicies",
            table: "Organization",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("UPDATE Organization SET UseViewPolicies = 1 WHERE PlanType IN(2,3,4,5,8,9,10,11,12,13,14,15,17,18,19,20);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UseViewPolicies",
            table: "Organization");
    }
}
