# Bitwarden library shape

This document is the canonical shape a library under `src/Libraries/` is expected
to follow. New libraries and reviewers can measure against it. See
[ADR-0031](https://contributing.bitwarden.com/architecture/adr/adopt-minimal-apis)
and [ADR-0032](https://contributing.bitwarden.com/architecture/adr/break-up-core)
for the motivating decisions.

## Public surface

Types in a library are `internal` by default. A type is only `public` when a
consumer outside the library legitimately needs it, and every `public` type
carries a documented contract: the guarantees a consumer can rely on and the
invariants callers must uphold.

The smallest reasonable public surface is two extension methods:

- `AddFoo(this IServiceCollection services)` — registers everything the library
  needs to run.
- `MapFooEndpoints(this IEndpointRouteBuilder builder)` — attaches the library's
  HTTP endpoints.

Many libraries never need more than these two entry points. Settings classes,
repository interfaces, and domain types become `public` only when a host or
another library must interact with them directly.

## Intentional public interface

### Settings

A library declares its own strongly-typed options class rather than extending
`GlobalSettings`. The options class is `public` so hosts can bind it and
consumers can inject `IOptions<FooSettings>`. The host — not the library —
owns the configuration section name and the binding call.

```csharp
public sealed class FooSettings
{
    public TimeSpan BarExpiration { get; set; } = TimeSpan.FromHours(4);
}

// In the library — no configuration knowledge inside AddFoo.
public static IServiceCollection AddFoo(this IServiceCollection services)
{
    // Library consumes IOptions<FooSettings>; host binds it.
    return services;
}

// In the host.
services.Configure<FooSettings>(configuration.GetSection("Foo"));
services.AddFoo();
```

Document the expected configuration shape — defaults, validation rules — on the
settings class itself with XML doc comments.

### Endpoints

Endpoints are minimal APIs defined inside the library. The library exposes a
single `MapFooEndpoints` extension on `IEndpointRouteBuilder`. The host owns
the route prefix; the library still owns the concerns that describe *what* it
is — authorization policies, tags, endpoint filters, versioning — and attaches
them to an empty `MapGroup("")` so every endpoint inside the library inherits
them uniformly.

```csharp
public static RouteGroupBuilder MapFooEndpoints(this IEndpointRouteBuilder builder)
{
    var group = builder.MapGroup("")
        .WithTags("Foo")
        .RequireAuthorization();

    group.MapGet("/{id}", GetFooAsync);
    group.MapPost("/", CreateFooAsync);

    return group;
}

// In the host — host owns "/foo" and nothing else.
app.MapGroup("/foo").MapFooEndpoints();
```

Return the library's own `RouteGroupBuilder` rather than the outer builder so
the host can chain further group-scoped configuration onto exactly what the
library mapped.

A library may also add a non-empty inner `MapGroup` when it needs a sub-prefix
shared by all of its endpoints. That is a deliberate choice on top of the empty
group, not a replacement for the host's outer prefix.

Endpoint handlers and endpoint filters live inside the library and stay `internal`
unless another library needs them.

### Repositories

A library owns its data access end-to-end: the repository interface, the Dapper
implementation for MSSQL, and the Entity Framework Core implementations for
PostgreSQL, MySQL, and SQLite. All of it lives inside the library.

The repository interface is `internal` when only the library consumes it.
`AddFoo` chooses the correct implementation based on the host's configured
database provider — the same dual-ORM pattern used elsewhere in the repo. If
the library does not need persistence, it has no data layer at all.

```csharp
public static IServiceCollection AddFoo(this IServiceCollection services)
{
    services.AddVaultDatabase();

    services.TryAddSingleton<DapperFooRepository>();
    services.TryAddSingleton<EntityFrameworkFooRepository>();
    services.TryAddSingleton<IFooRepository>(sp =>
    {
        var vaultDatabaseSettings = sp.GetRequiredService<IOptions<VaultDatabaseSettings>>().Value;

        if (vaultDatabaseSettings.PrefersDapper())
        {
            return sp.GetRequiredService<DapperFooRepository>();
        }

        return sp.GetRequiredService<EntityFrameworkFooRepository>();
    });

    return services;
}
```

## Composing your feature

`AddFoo` is where the library's internal services are wired together. Follow
the existing DI conventions:

- Register services with `TryAdd*` so hosts can pre-register test doubles or
  alternative implementations (ADR-0026).
- Register interfaces against internal implementations. External consumers only
  see the interfaces the library chose to expose.

`MapFooEndpoints` composes the library's endpoints under an inner
`RouteGroupBuilder`. Attach cross-cutting concerns — authorization policies,
endpoint filters, tags, API versioning — to the group so every endpoint in the
library inherits them uniformly.

## Depending on other features

Libraries interact with each other **only through public surface**. If
Library A needs behavior from Library B, it consumes B's public interfaces via
DI or calls B's public extension methods. Reaching into another library's
`internal` types is not allowed; if you find you need one, that is a
cross-team conversation with the owning team, not a hidden coupling to
work around.

`Core` is a transitional exception. A library may depend on `Core` while the
break-up is in progress, but it must document what it took from `Core` — the
specific types, services, or settings — so those pieces can be prioritized for
extraction. Treat every `Core` reference as a debt entry, not a permanent
choice. New libraries should sit below `Core` in the dependency graph and
avoid growing new dependencies on it wherever possible.
