using Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.Executions;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

public partial class EnableOrgsCollectionEnhancements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var execution = new OrganizationEnableCollectionEnhancementsExecution();
        execution.Run(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new Exception("Irreversible migration");
    }
}
