using Bit.Core.Platform.Data;
using Bit.Core.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Platform.Data;

/// <summary>
/// Integration tests for <see cref="ITransactionManager"/>. Verifies that ambient-transaction
/// behavior holds against every configured real RDBMS (SqlServer-Dapper, SqlServer-EF, Postgres,
/// MySql, Sqlite) — for the scope lifecycle (commit / rollback / dispose / exception), cross-
/// repository atomicity, nested-scope semantics (outer commit persists inner; inner rollback
/// dooms outer; inner commit is a no-op), AsyncLocal isolation across parallel flows, and the
/// no-ambient-scope passthrough that legacy callers rely on.
/// </summary>
public class TransactionManagerTests
{

    [Theory, DatabaseData]
    public async Task CommitAsync_PersistsWritesToDatabase(
        ITransactionManager transactionManager,
        IUserRepository userRepository)
    {
        var ct = TestContext.Current.CancellationToken;

        Guid userId;
        await using (var scope = await transactionManager.BeginTransactionAsync(cancellationToken: ct))
        {
            var user = await userRepository.CreateTestUserAsync();
            userId = user.Id;
            await scope.CommitAsync(ct);
        }

        Assert.NotNull(await userRepository.GetByIdAsync(userId));
    }

    [Theory, DatabaseData]
    public async Task RollbackAsync_DiscardsWrites(
        ITransactionManager transactionManager,
        IUserRepository userRepository)
    {
        var ct = TestContext.Current.CancellationToken;

        Guid userId;
        await using (var scope = await transactionManager.BeginTransactionAsync(cancellationToken: ct))
        {
            var user = await userRepository.CreateTestUserAsync();
            userId = user.Id;
            await scope.RollbackAsync(ct);
        }

        Assert.Null(await userRepository.GetByIdAsync(userId));
    }

    [Theory, DatabaseData]
    public async Task Dispose_WithoutCommit_AutomaticallyRollsBack(
        ITransactionManager transactionManager,
        IUserRepository userRepository)
    {
        var ct = TestContext.Current.CancellationToken;

        Guid userId;
        await using (await transactionManager.BeginTransactionAsync(cancellationToken: ct))
        {
            var user = await userRepository.CreateTestUserAsync();
            userId = user.Id;
            // Intentionally no Commit / Rollback — dispose must roll back.
        }

        Assert.Null(await userRepository.GetByIdAsync(userId));
    }

    [Theory, DatabaseData]
    public async Task ExceptionInScope_AutoRollsBackOnDispose(
        ITransactionManager transactionManager,
        IUserRepository userRepository)
    {
        var ct = TestContext.Current.CancellationToken;

        var userId = Guid.Empty;

        var exception = await Record.ExceptionAsync(async () =>
        {
            await using var scope = await transactionManager.BeginTransactionAsync(cancellationToken: ct);
            var user = await userRepository.CreateTestUserAsync();
            userId = user.Id;
            throw new InvalidOperationException("Synthetic failure");
        });

        Assert.IsType<InvalidOperationException>(exception);
        Assert.NotEqual(Guid.Empty, userId);
        Assert.Null(await userRepository.GetByIdAsync(userId));
    }

    [Theory, DatabaseData]
    public async Task SameScope_MultipleRepositories_AreAtomicOnCommit(
        ITransactionManager transactionManager,
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository)
    {
        var ct = TestContext.Current.CancellationToken;

        Guid userId;
        Guid organizationId;
        await using (var scope = await transactionManager.BeginTransactionAsync(cancellationToken: ct))
        {
            userId = (await userRepository.CreateTestUserAsync()).Id;
            organizationId = (await organizationRepository.CreateTestOrganizationAsync()).Id;
            await scope.CommitAsync(ct);
        }

        Assert.NotNull(await userRepository.GetByIdAsync(userId));
        Assert.NotNull(await organizationRepository.GetByIdAsync(organizationId));
    }

    [Theory, DatabaseData]
    public async Task SameScope_MultipleRepositories_AreAtomicOnRollback(
        ITransactionManager transactionManager,
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository)
    {
        var ct = TestContext.Current.CancellationToken;

        Guid userId;
        Guid organizationId;
        await using (var scope = await transactionManager.BeginTransactionAsync(cancellationToken: ct))
        {
            userId = (await userRepository.CreateTestUserAsync()).Id;
            organizationId = (await organizationRepository.CreateTestOrganizationAsync()).Id;
            await scope.RollbackAsync(ct);
        }

        Assert.Null(await userRepository.GetByIdAsync(userId));
        Assert.Null(await organizationRepository.GetByIdAsync(organizationId));
    }

    [Theory, DatabaseData]
    public async Task InScopeReads_SeeInFlightWritesFromSameTransaction(
        ITransactionManager transactionManager,
        IUserRepository userRepository)
    {
        var ct = TestContext.Current.CancellationToken;

        Guid userId;
        await using (var scope = await transactionManager.BeginTransactionAsync(cancellationToken: ct))
        {
            userId = (await userRepository.CreateTestUserAsync()).Id;

            // Inside the same scope (same connection / transaction) the read must see
            // the uncommitted write — proof that the read enrolled too.
            var readFromInside = await userRepository.GetByIdAsync(userId);
            Assert.NotNull(readFromInside);
            Assert.Equal(userId, readFromInside.Id);

            await scope.RollbackAsync(ct);
        }

        // After rollback the row must not be visible from a fresh (no-ambient) read.
        Assert.Null(await userRepository.GetByIdAsync(userId));
    }

    [Theory, DatabaseData]
    public async Task NestedScope_OuterCommit_PersistsInnerAndOuterWrites(
        ITransactionManager transactionManager,
        IUserRepository userRepository)
    {
        var ct = TestContext.Current.CancellationToken;

        Guid outerUserId;
        Guid innerUserId;

        await using (var outer = await transactionManager.BeginTransactionAsync(cancellationToken: ct))
        {
            outerUserId = (await userRepository.CreateTestUserAsync()).Id;

            await using (var inner = await transactionManager.BeginTransactionAsync(cancellationToken: ct))
            {
                innerUserId = (await userRepository.CreateTestUserAsync()).Id;
                await inner.CommitAsync(ct); // No-op; outer still owns the txn.
            }

            await outer.CommitAsync(ct);
        }

        Assert.NotNull(await userRepository.GetByIdAsync(outerUserId));
        Assert.NotNull(await userRepository.GetByIdAsync(innerUserId));
    }

    [Theory, DatabaseData]
    public async Task NestedScope_InnerRollback_DoomsOuterCommitAndDiscardsAllWrites(
        ITransactionManager transactionManager,
        IUserRepository userRepository)
    {
        var ct = TestContext.Current.CancellationToken;

        Guid outerUserId;
        Exception? commitException;

        await using (var outer = await transactionManager.BeginTransactionAsync(cancellationToken: ct))
        {
            outerUserId = (await userRepository.CreateTestUserAsync()).Id;

            await using (var inner = await transactionManager.BeginTransactionAsync(cancellationToken: ct))
            {
                await inner.RollbackAsync(ct); // Dooms the root transaction.
            }

            commitException = await Record.ExceptionAsync(() => outer.CommitAsync(ct));
        }

        Assert.IsType<InvalidOperationException>(commitException);
        Assert.Null(await userRepository.GetByIdAsync(outerUserId));
    }

    [Theory, DatabaseData]
    public async Task NestedScope_InnerCommitWithoutOuterCommit_DiscardsAllWrites(
        ITransactionManager transactionManager,
        IUserRepository userRepository)
    {
        var ct = TestContext.Current.CancellationToken;

        Guid outerUserId;
        Guid innerUserId;

        await using (await transactionManager.BeginTransactionAsync(cancellationToken: ct))
        {
            outerUserId = (await userRepository.CreateTestUserAsync()).Id;

            await using (var inner = await transactionManager.BeginTransactionAsync(cancellationToken: ct))
            {
                innerUserId = (await userRepository.CreateTestUserAsync()).Id;
                await inner.CommitAsync(ct); // No-op.
            }

            // Outer is disposed without committing — auto-rollback applies to everything,
            // including writes the inner scope "committed".
        }

        Assert.Null(await userRepository.GetByIdAsync(outerUserId));
        Assert.Null(await userRepository.GetByIdAsync(innerUserId));
    }

    [Theory, DatabaseData]
    public async Task ParallelScopes_OnDifferentAsyncFlows_AreIsolated(
        ITransactionManager transactionManager,
        IUserRepository userRepository)
    {
        var ct = TestContext.Current.CancellationToken;

        Assert.Null(TransactionState.Current);

        async Task<Guid> RunIsolatedScopeAsync()
        {
            // Fresh async flow — must start with no ambient holder.
            Assert.Null(TransactionState.Current);

            await using var scope = await transactionManager.BeginTransactionAsync(cancellationToken: ct);
            Assert.NotNull(TransactionState.Current);

            var user = await userRepository.CreateTestUserAsync();
            await scope.CommitAsync(ct);
            return user.Id;
        }

        var ids = await Task.WhenAll(
            Task.Run(RunIsolatedScopeAsync, ct),
            Task.Run(RunIsolatedScopeAsync, ct),
            Task.Run(RunIsolatedScopeAsync, ct));

        // The outer flow never opened a scope — its slot must remain clear.
        Assert.Null(TransactionState.Current);

        foreach (var id in ids)
        {
            Assert.NotNull(await userRepository.GetByIdAsync(id));
        }
    }

    [Theory, DatabaseData]
    public async Task TransactionState_IsClearedAfterScopeDisposed(
        ITransactionManager transactionManager)
    {
        var ct = TestContext.Current.CancellationToken;

        Assert.Null(TransactionState.Current);

        await using (await transactionManager.BeginTransactionAsync(cancellationToken: ct))
        {
            Assert.NotNull(TransactionState.Current);
        }

        Assert.Null(TransactionState.Current);
    }

    [Theory, DatabaseData]
    public async Task NoAmbientScope_RepositoriesContinueToWorkUnchanged(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository)
    {
        // No transactionManager.BeginTransactionAsync — legacy passthrough.
        Assert.Null(TransactionState.Current);

        var user = await userRepository.CreateTestUserAsync();
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        Assert.NotNull(await userRepository.GetByIdAsync(user.Id));
        Assert.NotNull(await organizationRepository.GetByIdAsync(organization.Id));
    }
}
