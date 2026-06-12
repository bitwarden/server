# Transaction Manager

Provider-agnostic ambient transactions that span multiple repository calls without
forcing callers to know whether the database is reached via Dapper (MSSQL) or
Entity Framework (Postgres / MySQL / SQLite).

A caller opens one scope via `ITransactionManager`, then makes ordinary repository
calls. Repositories routed through the new base-class helpers join that
transaction automatically on the same async flow. The same code works under
either backend.

> **Current coverage.** Only repositories that route through
> `BaseRepository.ExecuteWithConnectionAsync` (Dapper) or
> `BaseEntityFrameworkRepository.ExecuteWithContextAsync` (EF) enroll in the
> ambient transaction. The generic `Repository<T, TEntity, TId>` bases do, but
> many bespoke repository methods still open their own connection directly
> (e.g., `new SqlConnection(ConnectionString)` in
> `Infrastructure.Dapper/Repositories/`) and silently bypass ambient state.
> Before relying on `ITransactionManager` for a given flow, verify that every
> repository method it touches has been migrated to the helpers — otherwise
> those calls run outside the transaction.

## Files in this directory

| File | Role |
| --- | --- |
| `ITransactionManager.cs` | Public entry point. Resolved from DI. |
| `ITransactionScope.cs` | Scope handle returned from `BeginTransactionAsync`. `Commit`, `Rollback`, `DisposeAsync`. |
| `TransactionManagerBase.cs` | Shared logic: detect nesting, push the holder into ambient state, return the right scope type. |
| `TransactionState.cs` | `AsyncLocal<TransactionHolder?>` slot + the `TransactionHolder` that carries the connection / transaction / EF context for the current flow. |
| `RootTransactionScope.cs` | Outermost scope. Owns the real commit / rollback / dispose. |
| `NestedTransactionScope.cs` | Inner scope. Commit is a no-op; rollback dooms the root. |

Provider-specific subclasses live next to their respective infrastructure:

- `src/Infrastructure.Dapper/Data/DapperTransactionManager.cs`
- `src/Infrastructure.EntityFramework/Data/EfTransactionManager.cs`

Repository integration points:

- `src/Infrastructure.Dapper/Repositories/BaseRepository.cs` — `GetConnection()` /
  `ExecuteWithConnectionAsync(...)`
- `src/Infrastructure.EntityFramework/Repositories/BaseEntityFrameworkRepository.cs`
  — `ExecuteWithContextAsync(...)`

DI registration: `src/SharedWeb/Utilities/ServiceCollectionExtensions.cs`
(`AddDatabaseRepositories`). Registered as a singleton because all per-flow state
lives in `AsyncLocal`, not on the instance.

## How it works

### Ambient state via AsyncLocal

`TransactionState.Current` is a static `AsyncLocal<TransactionHolder?>`. The
holder carries the open `DbConnection`, the open `DbTransaction`, and (for EF)
the `DatabaseContext` plus the `IServiceScope` that owns it. Because the slot is
`AsyncLocal`, the holder flows through `await` boundaries but does not bleed
across unrelated requests.

Repositories never receive the holder as an argument. They look it up on each
call. If a holder exists, they use it; otherwise they open their own connection
or scope as before. That is what makes the manager opt-in: existing callers that
do not open a scope continue to work unchanged.

### Begin: root vs. nested

`TransactionManagerBase.BeginTransactionAsync` checks `TransactionState.Current`:

- **No holder present** → call the provider's `CreateRootHolderAsync` to open a
  connection and begin a transaction, store the holder in `TransactionState`,
  return a `RootTransactionScope`.
- **Holder already present** → return a `NestedTransactionScope` that wraps the
  existing holder. The `isolationLevel` argument is ignored because the inner
  call joins the outer transaction; honoring it would be a lie.

### Commit / rollback

- `RootTransactionScope.CommitAsync` — refuses to commit if a nested scope marked
  the transaction as `Doomed`; otherwise commits the underlying `DbTransaction`
  and sets `Committed = true` so dispose does not roll back.
- `RootTransactionScope.RollbackAsync` — dooms the transaction, rolls it back,
  and sets `RolledBack = true` so dispose does not roll back again.
- `RootTransactionScope.DisposeAsync` — clears `TransactionState.Current` and
  disposes the holder. If neither `Committed` nor `RolledBack` is true, the
  holder issues a best-effort rollback. EF holders also dispose the
  `IServiceScope` that owns the context.

- `NestedTransactionScope.CommitAsync` — no-op. Only the root commits.
- `NestedTransactionScope.RollbackAsync` — sets `Doomed = true` on the holder so
  the eventual root commit fails fast.
- `NestedTransactionScope.DisposeAsync` — no-op; the root owns lifetime.

This gives "rollback wins" semantics for nesting: if any inner scope rolls back,
the whole outer transaction is doomed even if the outer scope tries to commit.

### Connection ownership

`TransactionHolder.OwnsConnection` exists because Dapper and EF reach the
database differently:

- **Dapper** opens a fresh `SqlConnection`. The holder owns it and disposes it.
- **EF** uses the `DbConnection` that belongs to a `DatabaseContext` resolved
  from a child service scope. Disposing the connection out from under EF would
  break it, so the EF holder sets `OwnsConnection = false` and instead disposes
  the `IServiceScope` (`Scope` on the holder), which transitively disposes the
  context and its connection.

### Repository integration

Both base repositories follow the same shape: check ambient state, fall back to
the legacy per-call resource.

Dapper (`BaseRepository.cs`):

```csharp
var holder = TransactionState.Current;
if (holder is not null)
{
    return ((SqlConnection)holder.Connection, holder.Transaction, false /* not owned */);
}
return (new SqlConnection(ConnectionString), null, true /* owned, dispose on exit */);
```

Repositories that route through `ExecuteWithConnectionAsync` pass the resolved
`transaction` argument to Dapper's `ExecuteAsync` / `QueryAsync`. When no
ambient transaction is active, that argument is null and behavior is unchanged.
Repository methods that still open their own `SqlConnection` directly do not
participate; migrate them to `ExecuteWithConnectionAsync` before relying on
ambient enlistment.

Entity Framework (`BaseEntityFrameworkRepository.cs`):

```csharp
var holder = TransactionState.Current;
if (holder?.DbContext is DatabaseContext ambientContext)
{
    return (ambientContext, ownedScope: null);
}
var scope = ServiceScopeFactory.CreateScope();
return (GetDatabaseContext(scope), scope);
```

`EfTransactionManager` wires the open `DbTransaction` to the ambient
`DatabaseContext` via `Database.UseTransactionAsync` so `SaveChangesAsync()`
enrolls in it.

## Usage

Inject `ITransactionManager`, open a scope, do the work, commit:

```csharp
public class PostUserCommand(
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    ITransactionManager transactionManager)
{
    public async Task DoWorkAsync(Guid organizationId)
    {
        await using var scope = await transactionManager.BeginTransactionAsync(
            IsolationLevel.Serializable);

        var organization = await organizationRepository.GetByIdAsync(organizationId);
        // ... more repository calls — all join the same transaction
        await organizationUserRepository.CreateAsync(newUser);

        await scope.CommitAsync();
    }
}
```

Rules:

- `await using` the scope. Forgetting to dispose leaves an open transaction on
  the async flow.
- Call `CommitAsync` explicitly. Disposing without committing rolls back.
- Inner methods that also call `BeginTransactionAsync` will get a nested scope.
  They can `CommitAsync` it freely; only the outermost commit matters. If they
  `RollbackAsync` or throw, the outer scope cannot commit.

## Why a custom manager rather than `TransactionScope` or
`dbContext.Database.BeginTransactionAsync`?

- **One API across both ORMs.** Callers in `Bit.Core` and SCIM commands speak
  `ITransactionManager` regardless of whether they are running against MSSQL +
  Dapper or Postgres / MySQL / SQLite + EF.
- **Implicit enlistment.** Existing repositories did not need to grow new
  overloads accepting a transaction; they just consult ambient state.
- **No `System.Transactions` / DTC dependency.** `TransactionScope` would
  promote to a distributed transaction in several of our hosting configurations;
  we deliberately stay on the single-connection model.

## Lifetime and threading

- The manager is registered as a **singleton**. All per-flow state lives in
  `AsyncLocal`, not in fields on the manager.
- A transaction holder is bound to a single async flow. Do not stash a scope on
  a static, do not pass it across requests, and do not fan out to
  `Task.Run`-style background work and expect the ambient transaction to follow
  in a useful way — `AsyncLocal` will copy the reference, but the underlying
  `DbConnection` is not safe for concurrent use.
- The connection is opened when the root scope begins and stays open until the
  root scope is disposed. Keep scopes short.
