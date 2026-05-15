using System.Data;
using Bit.Core.Platform.Data;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Data;

public sealed class EfTransactionManager(IServiceScopeFactory serviceScopeFactory) : TransactionManagerBase
{
    protected override async Task<TransactionHolder> CreateRootHolderAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        var scope = serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken);

        await dbContext.Database.UseTransactionAsync(transaction, cancellationToken);

        return new TransactionHolder
        {
            Connection = connection,
            Transaction = transaction,
            OwnsConnection = false,
            DbContext = dbContext,
            Scope = scope,
        };
    }
}
