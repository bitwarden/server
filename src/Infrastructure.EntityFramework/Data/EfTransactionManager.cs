using System.Data;
using Bit.Core.Platform.Data;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Data;

public sealed class EfTransactionManager(IServiceScopeFactory serviceScopeFactory) : TransactionManagerBase
{
    protected override async Task InitializeRootHolderAsync(
        TransactionHolder holder,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        var scope = serviceScopeFactory.CreateAsyncScope();
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);
            var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken);
            await dbContext.Database.UseTransactionAsync(transaction, cancellationToken);

            holder.Initialize(connection, transaction, ownsConnection: false);
            holder.AttachDbContext(dbContext, scope);
        }
        catch
        {
            await scope.DisposeAsync();
            throw;
        }
    }
}
