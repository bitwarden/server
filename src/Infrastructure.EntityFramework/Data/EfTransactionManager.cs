using System.Data;
using Bit.Core.Platform.Data;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Data;

public sealed class EfTransactionManager : ITransactionManager
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public EfTransactionManager(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public bool HasActiveTransaction => TransactionState.Current is not null;

    public async Task<ITransactionScope> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var existing = TransactionState.Current;
        if (existing is not null)
        {
            existing.ReferenceCount++;
            return new NestedTransactionScope(existing);
        }

        var scope = _serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken);

        await dbContext.Database.UseTransactionAsync(transaction, cancellationToken);

        var holder = new TransactionHolder
        {
            Connection = connection,
            Transaction = transaction,
            OwnsConnection = false,
            DbContext = dbContext,
            Scope = scope,
        };
        TransactionState.Current = holder;
        return new RootTransactionScope(holder);
    }
}
