using Bit.Infrastructure.EntityFramework.Repositories;

namespace Bit.Infrastructure.IntegrationTest;

public interface ITestDatabaseHelper
{
    Database Info { get; }
    void ClearTracker();
}

public class EfTestDatabaseHelper : ITestDatabaseHelper
{
    private readonly DatabaseContext _databaseContext;

    public EfTestDatabaseHelper(DatabaseContext databaseContext, Database database)
    {
        _databaseContext = databaseContext;
        Info = database;
    }

    public Database Info { get; }

    public void ClearTracker()
    {
        _databaseContext.ChangeTracker.Clear();
    }
}

public class DapperSqlServerTestDatabaseHelper : ITestDatabaseHelper
{
    public DapperSqlServerTestDatabaseHelper(Database database)
    {
        Info = database;
    }

    public Database Info { get; }

    public void ClearTracker()
    {
        // There are no tracked entities in Dapper SQL Server
    }
}
