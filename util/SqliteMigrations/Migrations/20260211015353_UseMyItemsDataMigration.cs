using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class UseMyItemsDataMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
                UPDATE Organization
                SET UseMyItems = 1
                WHERE PlanType IN (4, 5, 10, 11, 14, 15, 19, 20);
            ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
                UPDATE Organization
                SET UseMyItems = 0
                WHERE PlanType IN (4, 5, 10, 11, 14, 15, 19, 20);
            ");
    }
}
