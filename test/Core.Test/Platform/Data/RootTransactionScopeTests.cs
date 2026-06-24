using System.Data.Common;
using Bit.Core.Platform.Data;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Platform.Data;

public class RootTransactionScopeTests
{
    private static (TransactionHolder holder, DbTransaction transaction) CreateHolder()
    {
        var connection = Substitute.For<DbConnection>();
        var transaction = Substitute.For<DbTransaction>();
        var holder = new TransactionHolder();
        holder.Initialize(connection, transaction, ownsConnection: false);
        return (holder, transaction);
    }

    [Fact]
    public async Task ExplicitRollback_ThenDispose_RollsBackExactlyOnce()
    {
        var (holder, transaction) = CreateHolder();
        var scope = new RootTransactionScope(holder);

        await scope.RollbackAsync();
        await scope.DisposeAsync();

        await transaction.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await transaction.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        Assert.True(holder.RolledBack);
        Assert.True(holder.Doomed);
    }

    [Fact]
    public async Task ExplicitCommit_ThenDispose_DoesNotRollBack()
    {
        var (holder, transaction) = CreateHolder();
        var scope = new RootTransactionScope(holder);

        await scope.CommitAsync();
        await scope.DisposeAsync();

        await transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await transaction.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
        Assert.True(holder.Committed);
    }

    [Fact]
    public async Task Dispose_WithoutCommitOrRollback_RollsBackExactlyOnce()
    {
        var (holder, transaction) = CreateHolder();
        var scope = new RootTransactionScope(holder);

        await scope.DisposeAsync();

        await transaction.Received(1).RollbackAsync();
        await transaction.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
