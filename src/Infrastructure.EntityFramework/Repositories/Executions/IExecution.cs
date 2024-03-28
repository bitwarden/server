using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.Infrastructure.EntityFramework.Repositories.Executions;

public interface IExecution
{
    /// <summary>
    /// Executes a command against the database.
    /// </summary>
    /// <returns>Number of rows affected.</returns>
    int Run(DatabaseContext dbContext);

    /// <summary>
    /// Executes a command against the database using the migration builder in the context of a migration.
    /// </summary>
    void Run(MigrationBuilder migrationBuilder);
}
