using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class UseInviteLinksDataMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
                UPDATE ""Organization""
                SET ""UseInviteLinks"" = true
                WHERE ""PlanType"" IN (4, 5, 10, 11, 14, 15, 19, 20);
            ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
                UPDATE ""Organization""
                SET ""UseInviteLinks"" = false
                WHERE ""PlanType"" IN (4, 5, 10, 11, 14, 15, 19, 20);
            ");
    }
}
