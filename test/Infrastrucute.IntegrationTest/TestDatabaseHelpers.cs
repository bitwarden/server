using Bit.Infrastructure.EntityFramework.Repositories;

namespace Bit.Infrastructure.IntegrationTest;

public interface ITestDatabaseHelper
{
    void ClearTracker();
}

public class EfTestDatabaseHelper : ITestDatabaseHelper
{
    private readonly DatabaseContext _databaseContext;

    public EfTestDatabaseHelper(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public void ClearTracker()
    {
        _databaseContext.ChangeTracker.Clear();
    }
}

public class DapperSqlServerTestDatabaseHelper : ITestDatabaseHelper
{
    public DapperSqlServerTestDatabaseHelper()
    {

    }

    public void ClearTracker()
    {
        // There are no tracked entities in Dapper SQL Server
    }
}
