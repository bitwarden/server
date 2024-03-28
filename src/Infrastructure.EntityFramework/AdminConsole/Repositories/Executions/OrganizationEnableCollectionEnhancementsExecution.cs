using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Executions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.Executions;

public class OrganizationEnableCollectionEnhancementsExecution : IExecution
{
    private const string _mySqlScript = "Infrastructure.EntityFramework.AdminConsole.Repositories.Executions.MySql.OrganizationEnableCollectionEnhancements.sql";
    private const string _postgresScript = "Infrastructure.EntityFramework.AdminConsole.Repositories.Executions.Postgres.OrganizationEnableCollectionEnhancements.psql";
    private const string _sqliteScript = "Infrastructure.EntityFramework.AdminConsole.Repositories.Executions.Sqlite.OrganizationEnableCollectionEnhancements.sql";

    public int Run(DatabaseContext dbContext)
    {
        string query;

        if (dbContext.Database.IsMySql())
        {
            query = CoreHelpers.GetEmbeddedResourceContentsAsync(_mySqlScript);
        }
        else if (dbContext.Database.IsNpgsql())
        {
            query = CoreHelpers.GetEmbeddedResourceContentsAsync(_postgresScript);
        }
        else if (dbContext.Database.IsSqlite())
        {
            query = CoreHelpers.GetEmbeddedResourceContentsAsync(_sqliteScript);
        }
        else
        {
            throw new NotSupportedException();
        }

        return dbContext.Database.ExecuteSqlRaw(query);
    }

    public void Run(MigrationBuilder migrationBuilder)
    {
        string query;

        if (migrationBuilder.IsMySql())
        {
            query = CoreHelpers.GetEmbeddedResourceContentsAsync(_mySqlScript);
        }
        else if (migrationBuilder.IsNpgsql())
        {
            query = CoreHelpers.GetEmbeddedResourceContentsAsync(_postgresScript);
        }
        else if (migrationBuilder.IsSqlite())
        {
            query = CoreHelpers.GetEmbeddedResourceContentsAsync(_sqliteScript);
        }
        else
        {
            throw new NotSupportedException();
        }

        migrationBuilder.Sql(query);
    }
}
