# Bitwarden Server Caching

Caching options available in Bitwarden's server. The server uses multiple caching layers and backends to balance performance, scalability, and operational simplicity across both cloud and self-hosted deployments.

---

## Choosing a Caching Option

Use this decision tree to identify the appropriate caching option for your feature:

```
Does your data need to be shared across all instances in a horizontally-scaled deployment?
├─ YES
│   │
│   Do you need long-term persistence with TTL (days/weeks)?
│   ├─ YES
│   │   │
│   │   Is it OK for a write on one instance to take a while to reach the
│   │   in-memory caches on other instances (no cross-instance invalidation)?
│   │   ├─ NO → Use `IDistributedCache` with persistent keyed service (every read hits the store)
│   │   └─ YES → Use `ExtendedCache` paired with the persistent cache
│   │             (in-memory cache + Cosmos/SQL/EF distributed cache —
│   │              see "Pairing ExtendedCache with Cosmos DB" below)
│   │
│   └─ NO → Use `ExtendedCache`
│       │
│       Notes:
│       - With Redis configured: memory + distributed + backplane
│       - With a non-Redis IDistributedCache registered: memory + distributed, no backplane
│       - With nothing registered: memory-only with stampede protection
│       - Provides fail-safe, eager refresh, circuit breaker
│       - For org/provider abilities: Use GetOrSetAsync with preloading pattern
│
└─ NO (single instance or manual sync acceptable)
    │
    Use `ExtendedCache` with memory-only mode (EnableDistributedCache = false)
    │
    Notes:
    - Same performance as raw IMemoryCache
    - Built-in stampede protection, eager refresh, fail-safe
    - "Free" Redis/backplane if needed at a later date (but not required)
    - Only use specialized in-memory cache if ExtendedCache API doesn't fit

*Stampede protection = prevents cache stampedes (multiple simultaneous requests for the same expired/missing key triggering redundant backend calls)
```

---

## Caching Options Overview

| Option                                 | Best For                                       | Horizontal Scale | TTL Support | Backend Options                |
| -------------------------------------- | ---------------------------------------------- | ---------------- | ----------- | ------------------------------ |
| **ExtendedCache**                      | General-purpose caching with advanced features | ✅ Yes           | ✅ Yes      | Redis, Cosmos, SQL, EF, Memory |
| **IDistributedCache** (default)        | Short-lived key-value caching                  | ✅ Yes           | ⚠️ Manual   | Redis, SQL, EF                 |
| **IDistributedCache** (`"persistent"`) | Long-lived data with TTL                       | ✅ Yes           | ✅ Yes      | Cosmos, Redis, SQL, EF         |
| **In-Memory Cache**                    | High-frequency reads, single instance          | ❌ No            | ⚠️ Manual   | Memory                         |

---

## `ExtendedCache`

`ExtendedCache` is a wrapper around [FusionCache](https://github.com/ZiggyCreatures/FusionCache) that provides a simple way to register **named, isolated caches** with sensible defaults. The goal is to make it trivial for each subsystem or feature to have its own cache - with optional distributed caching and backplane support - without repeatedly wiring up FusionCache, Redis, and related infrastructure.

> **Vocabulary**: throughout this section, **L1** refers to FusionCache's in-process memory cache, and **L2** refers to the configured `IDistributedCache` (Redis, Cosmos, SQL, EF, or none). FusionCache reads from L1 first, falls back to L2, and writes through to both.

Each named cache automatically receives:

- Its own `FusionCache` instance
- Its own configuration (default or overridden)
- Its own key prefix
- Optional distributed store
- Optional backplane

`ExtendedCache` supports four deployment modes:

- **Memory-only caching** (with stampede protection: prevents multiple concurrent requests for the same key from hitting the backend)
- **Memory + distributed cache + backplane** using the **shared** application Redis
- **Memory + distributed cache + backplane** using a **fully isolated** Redis instance
- **Memory + non-Redis distributed cache** (Cosmos DB, SQL Server, EF) — backplane unavailable (Redis-only feature)

### When to Use

- **General-purpose caching** for any domain data
- Features requiring **stampede protection** (when multiple concurrent requests for the same cache key should result in only a single backend call, with all requesters waiting for the same result)
- Data that benefits from **fail-safe mode** (serve stale data on backend failures)
- Multi-instance applications requiring **cache synchronization** via backplane
- You want **isolated cache configuration** per feature

### Pros

✅ **Advanced features out-of-the-box**:

- Stampede protection (multiple requests for same key = single backend call)
- Fail-safe mode with stale data serving
- Adaptive caching with eager refresh
- Automatic backplane support for multi-instance sync
- Circuit breaker for backend failures

✅ **Named, isolated caches**: Each feature gets its own cache instance with independent configuration

✅ **Flexible deployment modes**:

- Memory-only (development, testing)
- Memory + Redis (production cloud)
- Memory + isolated Redis (specialized features)

✅ **Simple API**: Uses `FusionCache`'s intuitive `GetOrSet` pattern

✅ **Built-in serialization**: Automatic JSON serialization/deserialization

### Cons

❌ Requires understanding of `FusionCache` configuration options

❌ Slightly more overhead than raw `IDistributedCache`

❌ IDistributedCache dependency for multi-instance deployments (typically Redis, but degrades gracefully to memory-only)

### Example Usage

**Note**: When using the shared Redis cache option (which is on by default, if the Redis connection string is configured), it is expected to call `services.AddDistributedCache(globalSettings)` **before** calling `AddExtendedCache`. The idea is to set up the distributed cache in our normal pattern and then "extend" it to include more functionality.

#### 1. Register the cache (in Startup.cs):

```csharp
// Option 1: Use default settings with shared Redis (if available)
services.AddDistributedCache(globalSettings);
services.AddExtendedCache("MyFeatureCache", globalSettings);

// Option 2: Memory-only mode for high-performance single-instance caching
services.AddExtendedCache("MyFeatureCache", globalSettings, new GlobalSettings.ExtendedCacheSettings
{
    EnableDistributedCache = false,  // Memory-only, same performance as IMemoryCache
    Duration = TimeSpan.FromHours(1),
    IsFailSafeEnabled = true,
    EagerRefreshThreshold = 0.9 // Refresh at 90% of TTL
});
// When EnableDistributedCache = false:
// - Uses memory-only caching (same performance as raw IMemoryCache)
// - Still provides stampede protection, eager refresh, fail-safe
// - Redis/backplane can be enabled later by setting EnableDistributedCache = true

// Option 3: Override default settings with Redis
services.AddExtendedCache("MyFeatureCache", globalSettings, new GlobalSettings.ExtendedCacheSettings
{
    Duration = TimeSpan.FromHours(1),
    IsFailSafeEnabled = true,
    FailSafeMaxDuration = TimeSpan.FromHours(2),
    EagerRefreshThreshold = 0.9 // Refresh at 90% of TTL
});

// Option 4: Isolated Redis for specialized features
services.AddExtendedCache("SpecializedCache", globalSettings, new GlobalSettings.ExtendedCacheSettings
{
    UseSharedDistributedCache = false,
    Redis = new GlobalSettings.ConnectionStringSettings
    {
        ConnectionString = "localhost:6379,ssl=false"
    }
});
// When configured this way:
// - A dedicated IConnectionMultiplexer is created
// - A dedicated IDistributedCache is created
// - A dedicated FusionCache backplane is created
// - All three are exposed to DI as keyed services (using the cache name as service key)

// Option 5: Non-Redis L2 (Cosmos DB, SQL Server, EF) — see "Backend Configuration" below
// Shared mode: the default unnamed IDistributedCache (whatever AddDistributedCache wired up)
// is reused as L2.
services.AddDistributedCache(globalSettings);
services.AddExtendedCache("MyFeatureCache", globalSettings, new GlobalSettings.ExtendedCacheSettings
{
    UseSharedDistributedCache = true,
    // No Redis.ConnectionString in globalSettings.DistributedCache.Redis
});

// Keyed mode: register an IDistributedCache under the cache name yourself, then call
// AddExtendedCache. This is how to pair ExtendedCache with the "persistent" (Cosmos)
// keyed service.
services.AddDistributedCache(globalSettings); // registers the "persistent" keyed cache —
                                              // Cosmos in cloud (when Cosmos.ConnectionString is set),
                                              // aliased to the unnamed IDistributedCache
                                              // (Redis / SQL / EF) in self-hosted
services.AddKeyedSingleton<IDistributedCache>(
    "MyLongLivedCache",
    (sp, _) => sp.GetRequiredKeyedService<IDistributedCache>("persistent"));
services.AddExtendedCache("MyLongLivedCache", globalSettings, new GlobalSettings.ExtendedCacheSettings
{
    UseSharedDistributedCache = false,
    Duration = TimeSpan.FromHours(24),
    // No settings.Redis.ConnectionString
});
// When configured this way (Option 5):
// - L1 (memory) + L2 (Cosmos/SQL/EF) caching with stampede protection, eager refresh, fail-safe
// - NO backplane (Redis-only). L1 entries on other instances will not be invalidated on write —
//   they expire on their own TTL. See "When NOT to Use" for the staleness tradeoff.
// - Keys are namespaced from other "persistent" cache consumers: ExtendedCache prefixes every
//   key with "MyLongLivedCache:", so entries cannot collide with the raw "persistent" namespace
//   used by direct AddDistributedCache consumers (payment workflow, OAuth grants).
```

#### 2. Inject and use the cache:

A named cache is retrieved via DI using keyed services (similar to how [IHttpClientFactory](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-7.0#named-clients) works with named clients):

```csharp
public class MyService
{
    private readonly IFusionCache _cache;
    private readonly IItemRepository _itemRepository;

    // Option A: Inject via keyed service in constructor
    public MyService(
        [FromKeyedServices("MyFeatureCache")] IFusionCache cache,
        IItemRepository itemRepository)
    {
        _cache = cache;
        _itemRepository = itemRepository;
    }

    // Option B: Request manually from service provider
    // cache = provider.GetRequiredKeyedService<IFusionCache>(serviceKey: "MyFeatureCache")

    // Option C: Inject IFusionCacheProvider and request the named cache
    // (similar to IHttpClientFactory pattern)
    public MyService(
        IFusionCacheProvider cacheProvider,
        IItemRepository itemRepository)
    {
        _cache = cacheProvider.GetCache("MyFeatureCache");
        _itemRepository = itemRepository;
    }

    public async Task<Item> GetItemAsync(Guid id)
    {
        return await _cache.GetOrSetAsync<Item>(
            $"item:{id}",
            async _ => await _itemRepository.GetByIdAsync(id),
            options => options.SetDuration(TimeSpan.FromMinutes(30))
        );
    }
}
```

`ExtendedCache` doesn't change how `FusionCache` is used in code, which means all the functionality and full `FusionCache` API is available. See the [FusionCache docs](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CoreMethods.md) for more details.

### Specific Example: SSO Authorization Grants

SSO authorization grants are **ephemeral, short-lived data** (typically ≤5 minutes) used to coordinate authorization flows across horizontally-scaled instances. `ExtendedCache` is ideal for this use case:

```csharp
services.AddExtendedCache("SsoGrants", globalSettings, new GlobalSettings.ExtendedCacheSettings
{
    Duration = TimeSpan.FromMinutes(5),
    IsFailSafeEnabled = false  // Re-initiate flow rather than serve stale grants
});

public class SsoAuthorizationService
{
    private readonly IFusionCache _cache;

    public SsoAuthorizationService([FromKeyedServices("SsoGrants")] IFusionCache cache)
    {
        _cache = cache;
    }

    public async Task<SsoGrant> GetGrantAsync(string authorizationCode)
    {
        return await _cache.GetOrDefaultAsync<SsoGrant>($"sso:grant:{authorizationCode}");
    }

    public async Task StoreGrantAsync(string authorizationCode, SsoGrant grant)
    {
        await _cache.SetAsync($"sso:grant:{authorizationCode}", grant);
    }
}
```

**Why `ExtendedCache` for SSO grants:**

- **Not critical if lost**: User can re-initiate SSO flow
- **Lower latency**: Redis backplane is faster than persistent storage
- **Simpler infrastructure**: Reuses existing Redis connection
- **Horizontal scaling**: Redis backplane automatically synchronizes across instances

### Backend Configuration

`ExtendedCache` works with any `IDistributedCache` as its L2 store. The chosen backend is resolved at registration time based on which knobs are set.

**Shared mode** (`UseSharedDistributedCache = true`, the default):

1. **Redis** — if `GlobalSettings.DistributedCache.Redis.ConnectionString` is configured. A shared `IConnectionMultiplexer`, `IDistributedCache`, and `IFusionCacheBackplane` are registered (or reused if already present).
2. **Whatever default `IDistributedCache` is registered** — if no Redis connection string is set, `ExtendedCache` looks up the unnamed `IDistributedCache` and uses it as L2. This is typically wired up by `services.AddDistributedCache(globalSettings)` and resolves to SQL Server or EF Cache in self-hosted deployments.
3. **Memory-only** — if neither Redis nor an `IDistributedCache` is registered.

**Keyed mode** (`UseSharedDistributedCache = false`):

1. **Dedicated Redis** — if `settings.Redis.ConnectionString` is set. A dedicated `IConnectionMultiplexer`, `IDistributedCache`, and `IFusionCacheBackplane` are registered under the cache name as the service key.
2. **Keyed `IDistributedCache`** — if no `settings.Redis.ConnectionString` is set, `ExtendedCache` looks up a keyed `IDistributedCache` registered under the cache name and uses it as L2. This is the path for wrapping Cosmos DB (or any other keyed `IDistributedCache`) under `ExtendedCache`.
3. **Memory-only** — if neither is registered.

> **Backplane caveat**: cross-instance cache invalidation only works with Redis (it uses Redis pub/sub). With a non-Redis L2 (Cosmos, SQL, EF), each instance's L1 memory cache is independent — writes from one instance do not invalidate L1 entries on other instances. Entries on other instances stay until their TTL expires. You still get stampede protection, eager refresh, and fail-safe within each instance.

#### Pairing `ExtendedCache` with Cosmos DB

Cosmos DB is registered by `AddDistributedCache` as the keyed `"persistent"` `IDistributedCache` in cloud deployments. To use it as L2 under `ExtendedCache`, register an alias under your cache name and use keyed mode without a Redis connection string (see Option 5 above). This gives you L1 memory + Cosmos L2.

**TTL.** FusionCache's `Duration` (and any per-call `SetDuration`) is translated to `DistributedCacheEntryOptions.AbsoluteExpirationRelativeToNow` and written by `CosmosCache` as a per-document `ttl` on each Cosmos item — that's the authoritative expiry per entry. The Cosmos container must have `DefaultTimeToLive` enabled (`-1` or `>0`) for per-document `ttl` to apply at all; the container value also serves as a default for items that omit `ttl`, but FusionCache always sets a per-item value so the container value never caps it.

**RU profile and the eager-refresh tradeoff.** L1 absorbs most reads, which is the win: hot keys see roughly one Cosmos point-read per process per TTL window instead of one per request. The cost is on the write side — `EagerRefreshThreshold` (default `0.9f`) produces a steady cadence of background factory calls plus L2 writes (~1 per hot key per `Duration` window) that raw `CosmosCache` does not have. If your access pattern doesn't benefit from background refresh (e.g. cold keys, low read concurrency), set `EagerRefreshThreshold = null` to disable it.

Cross-instance L1 invalidation is not provided — see the backplane caveat above.

### Specific Example: Organization/Provider Abilities

Organization and provider abilities are read extremely frequently (on every request that checks permissions) but change infrequently. `ExtendedCache` is ideal for this access pattern with its eager refresh and Redis backplane support:

```csharp
services.AddExtendedCache("OrganizationAbilities", globalSettings, new GlobalSettings.ExtendedCacheSettings
{
    Duration = TimeSpan.FromMinutes(10),
    EagerRefreshThreshold = 0.9,  // Refresh at 90% of TTL
    IsFailSafeEnabled = true,
    FailSafeMaxDuration = TimeSpan.FromHours(1)  // Serve stale data up to 1 hour on backend failures
});

public class OrganizationAbilityService
{
    private readonly IFusionCache _cache;
    private readonly IOrganizationRepository _organizationRepository;

    public OrganizationAbilityService(
        [FromKeyedServices("OrganizationAbilities")] IFusionCache cache,
        IOrganizationRepository organizationRepository)
    {
        _cache = cache;
        _organizationRepository = organizationRepository;
    }

    public async Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync()
    {
        return await _cache.GetOrSetAsync<IDictionary<Guid, OrganizationAbility>>(
            "all-org-abilities",
            async _ =>
            {
                var abilities = await _organizationRepository.GetManyAbilitiesAsync();
                return abilities.ToDictionary(a => a.Id);
            }
        );
    }

    public async Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid orgId)
    {
        var abilities = await GetOrganizationAbilitiesAsync();
        abilities.TryGetValue(orgId, out var ability);
        return ability;
    }

    public async Task UpsertOrganizationAbilityAsync(Organization organization)
    {
        // Update database
        await _organizationRepository.ReplaceAsync(organization);

        // Invalidate cache - with Redis backplane, this broadcasts to all instances
        await _cache.RemoveAsync("all-org-abilities");
    }
}
```

**Why `ExtendedCache` for org/provider abilities:**

- **High-frequency reads**: Every permission check reads abilities
- **Infrequent writes**: Abilities change rarely
- **Eager refresh**: Automatically refreshes at 90% of TTL to prevent cache misses
- **Fail-safe mode**: Serves stale data if database temporarily unavailable
- **Redis backplane**: Automatically invalidates across all instances when abilities change
- **No Service Bus dependency**: Simpler infrastructure (one Redis instead of Redis + Service Bus)

### Specific Example: Long-Lived Per-Tenant Computed Aggregates

Precomputed per-organization data — e.g. dashboard analytics, materialized usage summaries, or rolled-up compliance state — is expensive to compute, read many times per dashboard session, and stable for hours. Pairing `ExtendedCache` with the persistent Cosmos cache (see Option 5 above) puts an L1 memory cache in front of a durable Cosmos L2, so each instance amortizes the first recomputation across many subsequent reads, and snapshots survive process restarts.

```csharp
// Registration — aliases the keyed "persistent" cache (Cosmos in cloud) under the cache name
services.AddDistributedCache(globalSettings);
services.AddKeyedSingleton<IDistributedCache>(
    "OrganizationAnalytics",
    (sp, _) => sp.GetRequiredKeyedService<IDistributedCache>("persistent"));

services.AddExtendedCache("OrganizationAnalytics", globalSettings, new GlobalSettings.ExtendedCacheSettings
{
    UseSharedDistributedCache = false,
    Duration = TimeSpan.FromHours(6),
    EagerRefreshThreshold = 0.9f,                  // refresh in background at 90% of TTL
    IsFailSafeEnabled = true,
    FailSafeMaxDuration = TimeSpan.FromHours(24),  // serve stale up to 24h on factory failures
});

public class OrganizationAnalyticsService
{
    private readonly IFusionCache _cache;
    private readonly IOrganizationAnalyticsRepository _repository;

    public OrganizationAnalyticsService(
        [FromKeyedServices("OrganizationAnalytics")] IFusionCache cache,
        IOrganizationAnalyticsRepository repository)
    {
        _cache = cache;
        _repository = repository;
    }

    public async Task<OrganizationAnalyticsSnapshot> GetSnapshotAsync(Guid organizationId)
    {
        return await _cache.GetOrSetAsync<OrganizationAnalyticsSnapshot>(
            $"org:{organizationId}:analytics-snapshot",
            async _ => await _repository.ComputeSnapshotAsync(organizationId)
        );
    }

    public async Task InvalidateAsync(Guid organizationId)
    {
        // No backplane on Cosmos — this clears the current instance's L1 and removes the
        // entry from L2. Other instances continue serving their L1 entries until TTL.
        await _cache.RemoveAsync($"org:{organizationId}:analytics-snapshot");
    }
}
```

**Why pair `ExtendedCache` with Cosmos for this pattern:**

- **High per-key read frequency**: Each organization's snapshot is read repeatedly during a dashboard session (multiple widgets, drill-downs, refreshes). L1 absorbs these reads after the first hit per instance, so Cosmos sees roughly one point-read per process per key per TTL window.
- **High key cardinality**: One snapshot per organization. The total dataset can grow large enough that an in-process memory-only cache cannot hold it across instances — durable Cosmos L2 is what makes the L1 layer viable as a working set.
- **Cosmos durability across deploys**: Snapshots remain valid for hours. A process restart does not flush L2, so freshly-started instances warm L1 from Cosmos rather than triggering a thundering herd of recomputation.
- **Expensive factory**: Computing a snapshot involves joins across multiple tables. Stampede protection coalesces concurrent dashboard loads after a TTL boundary into a single recomputation, not N.
- **Eager refresh masks TTL boundaries**: At 90% of `Duration`, the next read returns immediately from L1 while a background factory call refreshes both L1 and L2. Users never observe hard-miss latency at the boundary.
- **Fail-safe keeps dashboards working through transient DB issues**: If the repository call errors, recent stale data is served instead of a user-facing failure.
- **Cross-instance staleness is acceptable**: Snapshot data may lag by minutes after an `InvalidateAsync` on another instance. Without a backplane, the stale L1 entry on instance B persists until its TTL expires — fine for analytics, not appropriate for transactional state.

### When NOT to Use

- **Long-term persistent data where cross-instance L1 staleness is unacceptable** — Use `IDistributedCache` with the `"persistent"` keyed service directly. `ExtendedCache` can be paired with Cosmos (see [Backend Configuration](#backend-configuration)), but every instance has its own L1 memory cache and there is no backplane for non-Redis backends, so a write on instance A will not invalidate the L1 entry on instance B until its TTL expires. If you need every read to reflect the latest persisted value across all instances, skip the L1 layer and go straight to the persistent `IDistributedCache`.
- **Custom caching logic** — If ExtendedCache's API doesn't fit your use case, consider specialized in-memory cache

---

## `IDistributedCache`

`IDistributedCache` provides two service registrations for different use cases:

1. **Default (unnamed) service** - For ephemeral, short-lived data
2. **Persistent cache** (keyed service: `"persistent"`) - For longer-lived data with structured TTL

### When to Use

**Default `IDistributedCache`**:

- **Legacy code** already using `IDistributedCache` (consider migrating to `ExtendedCache`)
- **Third-party integrations** requiring `IDistributedCache` interface
- **ASP.NET Core session storage** (framework dependency)
- You have **specific requirements** that ExtendedCache doesn't support

> **Note**: For new code, prefer `ExtendedCache` over default `IDistributedCache`. ExtendedCache can be configured with `EnableDistributedCache = false` to use memory-only caching with the same performance as raw `IMemoryCache`, while still providing stampede protection, fail-safe, and eager refresh.

**Persistent cache** (keyed service: `"persistent"`):

- **Critical data where memory loss would impact users** (refresh tokens, consent grants)
- **Long-lived structured data** with automatic TTL (days to weeks)
- **Long-lived OAuth/OIDC grants** that must survive application restarts
- **Payment intents** or workflow state that spans multiple requests
- Data requiring **automatic expiration** without manual cleanup
- **Large cache datasets** that benefit from external storage (e.g., thousands of refresh tokens)

### Pros

✅ **Standard ASP.NET Core interface**: Widely understood, well-documented

✅ **Multiple backend support**: Redis, SQL Server, Entity Framework, Cosmos DB

✅ **Automatic backend selection**: Picks the right backend based on configuration

✅ **Simple API**: Just `Get`, `Set`, `Remove`, `Refresh`

✅ **Minimal overhead**: No additional layers beyond the backend

✅ **Keyed services**: Separate configurations for different use cases

### Cons

❌ **No stampede protection**: Multiple requests = multiple backend calls

❌ **No fail-safe mode**: Backend unavailable = cache miss

❌ **No backplane**: Manual cache invalidation across instances

❌ **Manual serialization**: You handle JSON serialization (or use helpers)

❌ **Manual TTL management** (default service): Must track expiration manually

### Example Usage: Default (Ephemeral Data)

#### 1. Registration (already done in Api, Admin, Billing, Events, EventsProcessor, Identity, and Notifications Startup.cs files):

```csharp
services.AddDistributedCache(globalSettings);
```

#### 2. Inject and use for short-lived tokens:

```csharp
public class TwoFactorService
{
    private readonly IDistributedCache _cache;

    public TwoFactorService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<string> GetEmailTokenAsync(Guid userId)
    {
        var key = $"email-2fa:{userId}";
        var cached = await _cache.GetStringAsync(key);
        return cached;
    }

    public async Task SetEmailTokenAsync(Guid userId, string token)
    {
        var key = $"email-2fa:{userId}";
        await _cache.SetStringAsync(key, token, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        });
    }
}
```

#### 3. Using JSON helpers:

```csharp
using Bit.Core.Utilities;

public async Task<MyData> GetDataAsync(string key)
{
    return await _cache.TryGetValue<MyData>(key);
}

public async Task SetDataAsync(string key, MyData data)
{
    await _cache.SetAsync(key, data, new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
    });
}
```

### Example Usage: Persistent (Long-Lived Data)

The persistent cache is accessed via keyed service injection and is optimized for long-lived structured data with automatic TTL support.

#### Specific Example: Payment Workflow State

The persistent `IDistributedCache` service is appropriate for workflow state that spans multiple requests and needs automatic TTL cleanup.

```csharp
public class PaymentWorkflowCache(
    [FromKeyedServices("persistent")] IDistributedCache distributedCache) : IPaymentWorkflowCache
{
    public async Task SetPaymentSessionAsync(Guid userId, string sessionId)
    {
        // Bidirectional mapping for payment flow
        var byUserIdCacheKey = $"payment_session_for_user_{userId}";
        var bySessionIdCacheKey = $"user_for_payment_session_{sessionId}";

        // Note: No explicit TTL set here. Cosmos DB uses container-level TTL for automatic cleanup.
        // In cloud, Cosmos TTL handles expiration. In self-hosted, the cache backend manages TTL.
        await Task.WhenAll(
            distributedCache.SetStringAsync(byUserIdCacheKey, sessionId),
            distributedCache.SetStringAsync(bySessionIdCacheKey, userId.ToString()));
    }

    public async Task<string?> GetPaymentSessionForUserAsync(Guid userId)
    {
        var cacheKey = $"payment_session_for_user_{userId}";
        return await distributedCache.GetStringAsync(cacheKey);
    }

    public async Task<Guid?> GetUserForPaymentSessionAsync(string sessionId)
    {
        var cacheKey = $"user_for_payment_session_{sessionId}";
        var value = await distributedCache.GetStringAsync(cacheKey);
        if (string.IsNullOrEmpty(value) || !Guid.TryParse(value, out var userId))
        {
            return null;
        }
        return userId;
    }

    public async Task RemovePaymentSessionForUserAsync(Guid userId)
    {
        var cacheKey = $"payment_session_for_user_{userId}";
        await distributedCache.RemoveAsync(cacheKey);
    }
}
```

#### Specific Example: Long-Lived OAuth Grants

Long-lived OAuth grants (refresh tokens, consent grants, device codes) use the persistent `IDistributedCache` in **cloud** and `IGrantRepository` as a **database fallback for self-hosted** when persistent cache is not configured:

**Cloud (Bitwarden-hosted)**:

- Uses persistent `IDistributedCache` directly (backed by Cosmos DB)
- Automatic TTL via Cosmos DB container-level TTL

**Self-hosted**:

- Uses `IGrantRepository` as a database fallback when persistent cache backend is not available
- Stores grants in `Grant` database table with automatic expiration

**Grant type recommendations:**

| Grant Type               | Lifetime     | Durability Requirement | Recommended Storage | Rationale                                                                                   |
| ------------------------ | ------------ | ---------------------- | ------------------- | ------------------------------------------------------------------------------------------- |
| SSO authorization codes  | ≤5 min       | Ephemeral, can be lost | `ExtendedCache`     | User can re-initiate SSO flow if code is lost; short lifetime limits exposure window        |
| OIDC authorization codes | ≤5 min       | Ephemeral, can be lost | `ExtendedCache`     | OAuth spec allows user to retry authorization; code is single-use and short-lived           |
| PKCE code verifiers      | ≤5 min       | Ephemeral, can be lost | `ExtendedCache`     | Tied to authorization code lifecycle; can be regenerated if authorization is retried        |
| Refresh tokens           | Days-weeks   | Must persist           | Persistent cache    | Losing these forces user re-authentication; critical for seamless user experience           |
| Consent grants           | Weeks-months | Must persist           | Persistent cache    | User shouldn't have to re-consent frequently; loss degrades UX and trust                    |
| Device codes             | Days         | Must persist           | Persistent cache    | Device flow is async; losing codes breaks pending device authorizations with no recovery UX |

### Backend Configuration

The backend is automatically selected based on configuration and service key:

#### Default `IDistributedCache` (ephemeral)

**Cloud (Bitwarden-hosted)**:

- **Redis** only (always configured in cloud environments)

**Self-hosted priority order**:

1. **Redis** (if `GlobalSettings.DistributedCache.Redis.ConnectionString` is configured)
2. **SQL Server Cache table** (if database provider is SQL Server)
3. **Entity Framework Cache table** (for PostgreSQL, MySQL, SQLite)

#### Persistent cache (keyed service: `"persistent"`)

**Cloud (Bitwarden-hosted)**:

1. **Cosmos DB** (if `GlobalSettings.DistributedCache.Cosmos.ConnectionString` is configured)
   - Database: `cache`
   - Container: `default`
2. **Falls back to Redis**

**Self-hosted priority order**:

1. **Redis** (if configured)
2. **SQL Server Cache table** (if database provider is SQL Server)
3. **Entity Framework Cache table** (for PostgreSQL, MySQL, SQLite)

### Backend Details

#### Redis

```csharp
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = globalSettings.DistributedCache.Redis.ConnectionString;
});
```

**Used for**: Cloud (always), self-hosted (if configured)

- **Pros**: Fast, horizontally scalable, battle-tested
- **Cons**: Additional infrastructure dependency (self-hosted only)
- **TTL**: Via `AbsoluteExpiration` in cache entry options

#### SQL Server Cache Table (Self-hosted only)

```csharp
services.AddDistributedSqlServerCache(options =>
{
    options.ConnectionString = globalSettings.SqlServer.ConnectionString;
    options.SchemaName = "dbo";
    options.TableName = "Cache";
});
```

**Used for**: Self-hosted deployments without Redis

- **Pros**: No additional infrastructure, works with existing database
- **Cons**: Slower than Redis, adds load to database, less scalable
- **TTL**: Via `ExpiresAtTime` and `AbsoluteExpiration` columns

#### Entity Framework Cache (Self-hosted only)

```csharp
services.AddSingleton<IDistributedCache, EntityFrameworkCache>();
```

**Used for**: Self-hosted deployments with PostgreSQL, MySQL, or SQLite

- **Pros**: Works with any EF-supported database (PostgreSQL, MySQL, SQLite)
- **Cons**: Slower than Redis, requires periodic expiration scanning, adds DB load

**Features**:

- Thread-safe operations with mutex locks
- Automatic expiration scanning every 30 minutes
- Sliding and absolute expiration support
- Provider-specific duplicate key handling

**TTL**: Via `ExpiresAtTime` and `AbsoluteExpiration` columns with background scanning

#### Cosmos DB (Cloud only, persistent cache)

```csharp
services.AddKeyedSingleton<IDistributedCache, CosmosCache>("persistent", (provider, _) =>
{
    return new CosmosCache(new CosmosCacheOptions
    {
        DatabaseName = "cache",
        ContainerName = "default",
        ClientBuilder = cosmosClientBuilder
    });
});
```

**Used for**: Cloud persistent keyed service only

- **Pros**: Globally distributed, automatic TTL support via container-level TTL, optimized for long-lived data
- **Cons**: Cloud-only, higher latency than Redis

**TTL**: Cosmos DB container-level TTL (automatic cleanup, no scanning required)

### Comparison: Default vs Persistent

| Characteristic          | Default                        | Persistent cache (`"persistent"`)              |
| ----------------------- | ------------------------------ | ---------------------------------------------- |
| **Primary Use Case**    | Ephemeral tokens, session data | Long-lived grants, workflow state              |
| **Typical TTL**         | 5-15 minutes                   | Hours to weeks                                 |
| **User Impact if Lost** | Low (user can retry)           | High (forces re-auth, interrupts workflows)    |
| **Scale Consideration** | Small datasets                 | Large/growing datasets (thousands to millions) |
| **Cloud Backend**       | Redis                          | Cosmos DB → Redis                              |
| **Self-Hosted Backend** | Redis → SQL → EF               | Redis → SQL → EF                               |
| **Automatic Cleanup**   | Manual expiration              | Automatic TTL (Cosmos)                         |
| **Data Structure**      | Simple key-value               | Supports structured data                       |
| **Example**             | 2FA codes, TOTP tokens         | Refresh tokens, payment intents                |

### Choosing Default vs Persistent

**Use Default when**:

- Data lifetime < 15 minutes
- Ephemeral authentication tokens
- Simple key-value pairs
- Cost optimization is important
- Data loss on restart is acceptable

**Use Persistent when**:

- **Data loss would have user impact** (e.g., losing refresh tokens forces re-authentication)
- Data lifetime > 15 minutes
- **Cache size is large or growing** (thousands of items that exceed memory constraints)
- Structured data with relationships
- Automatic TTL cleanup is required
- Data must survive restarts and deployments
- Query capabilities are needed (via Cosmos DB)

### When NOT to Use

- **New general-purpose caching** - Use `ExtendedCache` instead for stampede protection, fail-safe, and backplane support
- **Organization/Provider abilities** - Use `ExtendedCache` with preloading pattern (see example above)
- **Short-lived ephemeral data** without persistence requirements - Use `ExtendedCache` (simpler, more features)

---

## `IApplicationCacheService` (Deprecated)

> **⚠️ Deprecated**: This service is being phased out in favor of `ExtendedCache`. New code should use `ExtendedCache` with the preloading pattern shown in the [Organization/Provider Abilities example](#specific-example-organizationprovider-abilities) above.

### Background

`IApplicationCacheService` was a **highly domain-specific caching service** built for Bitwarden organization and provider abilities. It used in-memory cache with Azure Service Bus for cross-instance invalidation.

**Why it's being replaced:**

- **Infrastructure complexity**: Required both Redis and Azure Service Bus
- **Limited applicability**: Only worked for org/provider abilities
- **Maintenance burden**: Custom implementation instead of leveraging standard caching primitives
- **Better alternative exists**: `ExtendedCache` with Redis backplane provides the same functionality with simpler infrastructure

### Migration Path

**Old approach** (IApplicationCacheService):

- In-memory cache with periodic refresh
- Azure Service Bus for cross-instance invalidation
- Custom implementation for each domain

**New approach** (ExtendedCache):

- Memory + Redis distributed cache with backplane
- Eager refresh for automatic background updates
- Fail-safe mode for resilience
- Standard FusionCache API
- One Redis instance instead of Redis + Service Bus

See the [Organization/Provider Abilities example](#specific-example-organizationprovider-abilities) for the recommended migration pattern.

### When NOT to Use

❌ **Do not use for new code** - Use `ExtendedCache` instead

For existing code using `IApplicationCacheService`, plan migration to `ExtendedCache` using the pattern shown above.

---

## Specialized In-Memory Cache

> **Recommendation**: In most cases, use `ExtendedCache` with `EnableDistributedCache = false` instead of implementing a specialized in-memory cache. ExtendedCache provides the same memory-only performance with built-in stampede protection, eager refresh, and fail-safe capabilities.

### When to Use

Use a specialized in-memory cache only when:

- **ExtendedCache's API doesn't fit** your specific use case
- **Custom eviction logic** is required beyond TTL-based expiration
- **Non-standard data structures** (e.g., priority queues, LRU with custom scoring)
- **Direct memory access patterns** that bypass serialization entirely

For general high-performance caching, prefer `ExtendedCache` with memory-only mode.

### Pros

✅ **Maximum performance**: No serialization, no network calls, no locking overhead

✅ **Simple implementation**: Just a `Dictionary` or `ConcurrentDictionary`

✅ **Zero infrastructure**: No Redis, no database, no additional dependencies

### Cons

❌ **No horizontal scaling**: Each instance has separate cache state

❌ **Manual invalidation**: No built-in cache invalidation mechanism

❌ **Manual TTL**: You implement expiration logic

❌ **Memory pressure**: Large datasets can cause GC issues

### Example Implementation

#### Simple in-memory cache:

```csharp
public class MyFeatureCache
{
    private readonly ConcurrentDictionary<string, CacheEntry<MyData>> _cache = new();
    private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(30);

    public MyData GetOrAdd(string key, Func<MyData> factory)
    {
        var entry = _cache.GetOrAdd(key, _ => new CacheEntry<MyData>
        {
            Value = factory(),
            ExpiresAt = DateTime.UtcNow + _defaultExpiration
        });

        // WARNING: This implementation has a race condition. Multiple threads detecting
        // expiration simultaneously may each call TryRemove and then recursively call
        // GetOrAdd, potentially causing the factory to execute multiple times. For
        // production use cases requiring thread-safe expiration, consider using
        // IMemoryCache with GetOrCreateAsync or ExtendedCache with stampede protection.
        if (entry.ExpiresAt < DateTime.UtcNow)
        {
            _cache.TryRemove(key, out _);
            return GetOrAdd(key, factory);
        }

        return entry.Value;
    }

    private class CacheEntry<T>
    {
        public T Value { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
```

#### Using `IMemoryCache`:

```csharp
public class MyService
{
    private readonly IMemoryCache _memoryCache;

    public MyService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public async Task<MyData> GetDataAsync(string key)
    {
        return await _memoryCache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            entry.SetPriority(CacheItemPriority.High);

            return await _repository.GetDataAsync(key);
        });
    }
}
```

### When NOT to Use

- **Most general-purpose caching** - Use `ExtendedCache` with memory-only mode instead
- **Data requiring stampede protection** - Use `ExtendedCache`
- **Multi-instance deployments** requiring consistency - Use `ExtendedCache` with Redis
- **Long-lived OAuth grants** - Use persistent `IDistributedCache`

> **Important**: Before implementing a custom in-memory cache, first try `ExtendedCache` with `EnableDistributedCache = false`. This gives you memory-only performance with automatic stampede protection, eager refresh, and fail-safe mode.

---

## Backend Configuration

### Configuration Priority

The following table shows how different caching options resolve to storage backends based on configuration:

| Cache Option                           | Cloud Backend                                    | Self-Hosted Backend                          | Config Setting                                            |
| -------------------------------------- | ------------------------------------------------ | -------------------------------------------- | --------------------------------------------------------- |
| **ExtendedCache** [†](#extended-cache-cosmos-footnote)                      | Redis → registered `IDistributedCache` → Memory  | Redis → registered `IDistributedCache` (SQL/EF) → Memory | `GlobalSettings.DistributedCache.Redis.ConnectionString` + any pre-registered `IDistributedCache` |
| **IDistributedCache** (default)        | Redis                                            | Redis → SQL → EF                             | `GlobalSettings.DistributedCache.Redis.ConnectionString`  |
| **IDistributedCache** (`"persistent"`) | Cosmos → Redis                                   | Redis → SQL → EF                             | `GlobalSettings.DistributedCache.Cosmos.ConnectionString` |
| **OAuth Grants** (long-lived)          | Persistent cache (Cosmos)                        | `IGrantRepository` (SQL/EF)                  | Various (see above)                                       |

<a id="extended-cache-cosmos-footnote"></a>
† This row shows the backend `ExtendedCache` resolves to automatically. `ExtendedCache` can also be wired to Cosmos (or any keyed `IDistributedCache`) by opting into keyed mode and aliasing under the cache name — see [Option 5](#example-usage) and the [Pairing `ExtendedCache` with Cosmos DB](#pairing-extendedcache-with-cosmos-db) subsection.

### Redis Configuration

**Cloud (Bitwarden-hosted)**:

```json
{
  "GlobalSettings": {
    "DistributedCache": {
      "Redis": {
        "ConnectionString": "redis.example.com:6379,ssl=true,password=..."
      }
    }
  }
}
```

**Self-hosted** (`appsettings.json`):

```json
{
  "globalSettings": {
    "distributedCache": {
      "redis": {
        "connectionString": "localhost:6379"
      }
    }
  }
}
```

### Cosmos DB Configuration

**Persistent `IDistributedCache`** (cloud only):

```json
{
  "GlobalSettings": {
    "DistributedCache": {
      "Cosmos": {
        "ConnectionString": "AccountEndpoint=https://...;AccountKey=..."
      }
    }
  }
}
```

- Database: `cache`
- Container: `default`
- Used for long-lived grants in cloud deployments

### SQL Server Cache

**Automatic configuration** (if SQL Server is database provider):

```json
{
  "globalSettings": {
    "sqlServer": {
      "connectionString": "Server=...;Database=...;User Id=...;Password=..."
    }
  }
}
```

- Schema: `dbo`
- Table: `Cache`
- Migrations: Applied automatically

### Entity Framework Cache

**Automatic fallback** for PostgreSQL, MySQL, SQLite:

No additional configuration required. Uses existing database connection.

- Table: `Cache`
- Migrations: Applied automatically

---

## Performance Considerations

### Performance Characteristics

| Backend              | Read Latency | Write Latency | Throughput    |
| -------------------- | ------------ | ------------- | ------------- |
| **Memory**           | <1ms         | <1ms          | >100K req/s   |
| **Redis**            | 1-5ms        | 1-5ms         | 10K-50K req/s |
| **SQL Server**       | 5-20ms       | 10-50ms       | 1K-5K req/s   |
| **Entity Framework** | 5-20ms       | 10-50ms       | 1K-5K req/s   |
| **Cosmos DB**        | 5-15ms       | 5-15ms        | 10K+ req/s    |

**Note**: Latencies represent typical p95 values in production environments. Redis latencies assume same-datacenter deployment and include serialization overhead. Actual performance varies based on network topology, data size, and load.

### Recommendations

**For high-frequency reads (>1K req/s)**:

1. `ExtendedCache` with Redis (cloud)
2. `ExtendedCache` memory-only (self-hosted, single instance)
3. Specialized in-memory cache (extreme performance requirements)

**For moderate traffic (100-1K req/s)**:

1. `ExtendedCache` with shared Redis
2. `IDistributedCache` with SQL Server cache

**For low traffic (<100 req/s)**:

1. `IDistributedCache` with SQL Server / EF cache
2. `ExtendedCache` memory-only

---

## Testing Caches

### Unit Testing

**`ExtendedCache`**:

```csharp
[Fact]
public async Task TestCacheHit()
{
    var services = new ServiceCollection();
    services.AddMemoryCache();
    services.AddExtendedCache("TestCache", new GlobalSettings
    {
        DistributedCache = new GlobalSettings.DistributedCacheSettings()
    });

    var provider = services.BuildServiceProvider();
    var cache = provider.GetRequiredKeyedService<IFusionCache>("TestCache");

    await cache.SetAsync("key", "value");
    var result = await cache.GetOrDefaultAsync<string>("key");

    Assert.Equal("value", result);
}
```

**`IDistributedCache`**:

```csharp
[Fact]
public async Task TestDistributedCache()
{
    var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    await cache.SetStringAsync("key", "value");
    var result = await cache.GetStringAsync("key");

    Assert.Equal("value", result);
}
```

### Integration Testing

**Example**:

```csharp
[DatabaseTheory, DatabaseData]
public async Task Cache_ExpirationScanning_RemovesExpiredItems(IDistributedCache cache)
{
    // Set item with 1-second expiration
    await cache.SetAsync("key", Encoding.UTF8.GetBytes("value"), new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1)
    });

    // Wait for expiration
    await Task.Delay(TimeSpan.FromSeconds(2));

    // Trigger expiration scan
    var entityCache = cache as EntityFrameworkCache;
    await entityCache.ScanForExpiredItemsAsync();

    // Verify item is removed
    var result = await cache.GetAsync("key");
    Assert.Null(result);
}
```

---

## Migration Examples

Examples of migrating from one caching option to another:

### From `IDistributedCache` → `ExtendedCache`

**Before**:

```csharp
// Registration
services.AddDistributedCache(globalSettings);

// Constructor
public MyService(IDistributedCache cache, IRepository repository)
{
    _cache = cache;
    _repository = repository;
}

// Usage
public async Task<MyData> GetDataAsync(string key)
{
    var data = await _cache.TryGetValue<MyData>(key);
    if (data == null)
    {
        data = await _repository.GetAsync(key);
        await _cache.SetAsync(key, data, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        });
    }
    return data;
}
```

**After**:

```csharp
// Registration
services.AddDistributedCache(globalSettings);
services.AddExtendedCache("MyFeature", globalSettings);

// Constructor
public MyService(
    [FromKeyedServices("MyFeature")] IFusionCache cache,
    IRepository repository)
{
    _cache = cache;
    _repository = repository;
}

// Usage
public async Task<MyData> GetDataAsync(string key)
{
    return await _cache.GetOrSetAsync(
        key,
        async _ => await _repository.GetAsync(key),
        options => options.SetDuration(TimeSpan.FromMinutes(30))
    );
}
```

### From In-Memory → `ExtendedCache`

**Before**:

```csharp
// Field
private readonly ConcurrentDictionary<string, MyData> _cache = new();
private readonly IRepository _repository;

// Constructor
public MyService(IRepository repository)
{
    _repository = repository;
}

// Usage
public async Task<MyData> GetDataAsync(string key)
{
    if (_cache.TryGetValue(key, out var cached))
    {
        return cached;
    }

    var data = await _repository.GetAsync(key);
    _cache.TryAdd(key, data);
    return data;
}
```

**After**:

```csharp
// Registration
services.AddExtendedCache("MyFeature", globalSettings);

// Constructor
public MyService(
    [FromKeyedServices("MyFeature")] IFusionCache cache,
    IRepository repository)
{
    _cache = cache;
    _repository = repository;
}

// Usage
public async Task<MyData> GetDataAsync(string key)
{
    return await _cache.GetOrSetAsync(
        key,
        async _ => await _repository.GetAsync(key)
    );
}
```
