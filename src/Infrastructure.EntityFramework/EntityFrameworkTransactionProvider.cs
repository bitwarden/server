using System.Data;
using System.Data.Common;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework;

public class EntityFrameworkTransactionProvider(IServiceScopeFactory serviceScopeFactory) : ISqlTransactionProvider
{
    public async Task<DbTransaction> GetTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        return (await dbContext.Database.BeginTransactionAsync()).GetDbTransaction();
    }
}
