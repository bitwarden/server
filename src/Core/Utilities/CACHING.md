# Bitwarden Server Caching

Caching options available in Bitwarden's server. The server uses multiple caching layers and backends to balance performance, scalability, and operational simplicity across both cloud and self-hosted deployments.

---

## Choosing a Caching Option

Use this decision tree to identify the appropriate caching option for your feature:

```
Is it organization or provider abilities data?
├─ YES → Use `IApplicationCacheService`
└─ NO
    │
    Does your data need to be shared across all instances in a horizontally-scaled deployment?
    ├─ YES
    │   │
    │   Do you need advanced cache features (stampede protection*, fail-safe, backplane)?
    │   ├─ YES → Use `ExtendedCache`
    │   └─ NO
    │       │
    │       Do you need long-term persistence with TTL (days/weeks)?
    │       ├─ YES → Use `IDistributedCache` with persistent keyed service
    │       └─ NO → Use `IDistributedCache` default
    │
    └─ NO (single instance or manual sync acceptable)
        │
        Do you need sub-second performance for high-frequency reads?
        ├─ YES → Use Specialized In-Memory Cache
        └─ NO → Use `ExtendedCache` with memory-only mode

*Stampede protection = prevents cache stampedes (multiple simultaneous requests for the same expired/missing key triggering redundant backend calls)
```

---

## Caching Options Overview

| Option                                 | Best For                                       | Horizontal Scale | TTL Support | Backend Options        |
| -------------------------------------- | ---------------------------------------------- | ---------------- | ----------- | ---------------------- |
| **ExtendedCache**                      | General-purpose caching with advanced features | ✅ Yes           | ✅ Yes      | Redis, Memory          |
| **IDistributedCache** (default)        | Short-lived key-value caching                  | ✅ Yes           | ⚠️ Manual   | Redis, SQL, EF         |
| **IDistributedCache** (`"persistent"`) | Long-lived data with TTL                       | ✅ Yes           | ✅ Yes      | Cosmos, Redis, SQL, EF |
| **IApplicationCacheService**           | Org/Provider abilities                         | ✅ Yes           | ❌ No       | Memory + Service Bus   |
| **In-Memory Cache**                    | High-frequency reads, single instance          | ❌ No            | ⚠️ Manual   | Memory                 |

---

## `ExtendedCache`

`ExtendedCache` is a wrapper around [FusionCache](https://github.com/ZiggyCreatures/FusionCache) that provides a simple way to register **named, isolated caches** with sensible defaults. The goal is to make it trivial for each subsystem or feature to have its own cache - with optional distributed caching and backplane support - without repeatedly wiring up FusionCache, Redis, and related infrastructure.

Each named cache automatically receives:

- Its own `FusionCache` instance
- Its own configuration (default or overridden)
- Its own key prefix
- Optional distributed store
- Optional backplane

`ExtendedCache` supports three deployment modes:

- **Memory-only caching** (with stampede protection: prevents multiple concurrent requests for the same key from hitting the backend)
- **Memory + distributed cache + backplane** using the **shared** application Redis
- **Memory + distributed cache + backplane** using a **fully isolated** Redis instance

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

❌ Redis dependency for multi-instance deployments (but degrades gracefully to memory-only)

### Example Usage

**Note**: When using the shared Redis cache option (which is on by default, if the Redis connection string is configured), it is expected to call `services.AddDistributedCache(globalSettings)` **before** calling `AddExtendedCache`. The idea is to set up the distributed cache in our normal pattern and then "extend" it to include more functionality.

#### 1. Register the cache (in Startup.cs):

```csharp
// Option 1: Use default settings with shared Redis
services.AddDistributedCache(globalSettings);
services.AddExtendedCache("MyFeatureCache", globalSettings);

// Option 2: Override default settings
services.AddExtendedCache("MyFeatureCache", globalSettings, new GlobalSettings.ExtendedCacheSettings
{
    Duration = TimeSpan.FromHours(1),
    IsFailSafeEnabled = true,
    FailSafeMaxDuration = TimeSpan.FromHours(2),
    EagerRefreshThreshold = 0.9 // Refresh at 90% of TTL
});

// Option 3: Isolated Redis for specialized features
services.AddExtendedCache("SpecializedCache", globalSettings, new GlobalSettings.ExtendedCacheSettings
{
    UseSharedRedisCache = false,
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

`ExtendedCache` automatically uses the configured backend:

**Cloud (Bitwarden-hosted)**:

1. Redis (primary, if `GlobalSettings.DistributedCache.Redis.ConnectionString` configured)
2. Memory-only (fallback if Redis unavailable)

**Self-hosted**:

1. Redis (if configured in `appsettings.json`)
2. Memory-only (default fallback)

### When NOT to Use

- Organization/Provider abilities (use `IApplicationCacheService` instead)
- Extremely high-frequency reads (>10K req/s) where serialization overhead matters (use in-memory)

---

## `IDistributedCache`

`IDistributedCache` provides two service registrations for different use cases:

1. **Default (unnamed) service** - For ephemeral, short-lived data
2. **Persistent cache** (keyed service: `"persistent"`) - For longer-lived data with structured TTL

### When to Use

**Default `IDistributedCache`**:

- **Simple key-value caching** without advanced features
- **Ephemeral data** (2FA codes, TOTP tokens, email tokens, replay prevention)
- **Short-lived data** (≤15 minutes) that doesn't need fail-safe or stampede protection
- **Authentication session tickets**
- You need **direct control** over serialization and expiration

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

#### 1. Registration (already done in all Startup.cs files):

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
public class SetupIntentDistributedCache(
    [FromKeyedServices("persistent")] IDistributedCache distributedCache) : ISetupIntentCache
{
    public async Task Set(Guid subscriberId, string setupIntentId)
    {
        // Bidirectional mapping for payment flow
        var bySubscriberIdCacheKey = $"setup_intent_id_for_subscriber_id_{subscriberId}";
        var bySetupIntentIdCacheKey = $"subscriber_id_for_setup_intent_id_{setupIntentId}";

        // Note: No explicit TTL set here. Cosmos DB uses container-level TTL for automatic cleanup.
        // In cloud, Cosmos TTL handles expiration. In self-hosted, the cache backend manages TTL.
        await Task.WhenAll(
            distributedCache.SetStringAsync(bySubscriberIdCacheKey, setupIntentId),
            distributedCache.SetStringAsync(bySetupIntentIdCacheKey, subscriberId.ToString()));
    }

    public async Task<string?> GetSetupIntentIdForSubscriber(Guid subscriberId)
    {
        var cacheKey = $"setup_intent_id_for_subscriber_id_{subscriberId}";
        return await distributedCache.GetStringAsync(cacheKey);
    }

    public async Task<Guid?> GetSubscriberIdForSetupIntent(string setupIntentId)
    {
        var cacheKey = $"subscriber_id_for_setup_intent_id_{setupIntentId}";
        var value = await distributedCache.GetStringAsync(cacheKey);
        if (string.IsNullOrEmpty(value) || !Guid.TryParse(value, out var subscriberId))
        {
            return null;
        }
        return subscriberId;
    }

    public async Task RemoveSetupIntentForSubscriber(Guid subscriberId)
    {
        var cacheKey = $"setup_intent_id_for_subscriber_id_{subscriberId}";
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

- Data requiring stampede protection (use `ExtendedCache`)
- Organization/Provider abilities (use `IApplicationCacheService`)
- Extremely high-frequency reads where serialization overhead matters (use in-memory cache)

---

## `IApplicationCacheService`

> **Note**: This is a **highly domain-specific caching service** for Bitwarden organization and provider abilities. It is built on top of the core caching primitives (in-memory cache + Service Bus for invalidation) and is **not implemented for most domains**.

### When to Use

This service is **pre-built for specific Bitwarden domains** and should only be used for:

- **Organization abilities** (feature flags, enabled features, billing status)
- **Provider abilities** (provider-level feature flags and configuration)

This service is **highly specific to these domains** and is not implemented for other use cases. For new caching needs, use `ExtendedCache` or `IDistributedCache` directly.

### How It Works

Organization and provider abilities are read extremely frequently (every request that checks permissions) but change infrequently. This service optimizes for this access pattern by:

1. Loading all abilities into memory on startup
2. Serving reads from in-memory cache (no database calls)
3. Invalidating cache across all instances when abilities change (via Service Bus)
4. Periodically refreshing to catch missed updates

### Example Usage

```csharp
public class OrganizationService
{
    private readonly IApplicationCacheService _applicationCacheService;

    public OrganizationService(IApplicationCacheService applicationCacheService)
    {
        _applicationCacheService = applicationCacheService;
    }

    public async Task<OrganizationAbility> GetOrganizationAbilityAsync(Guid orgId)
    {
        return await _applicationCacheService.GetOrganizationAbilityAsync(orgId);
    }

    public async Task UpdateOrganizationAsync(Organization org)
    {
        // Update database
        await _organizationRepository.ReplaceAsync(org);

        // Invalidate cache (broadcasts to all instances via Service Bus)
        await _applicationCacheService.UpsertOrganizationAbilityAsync(org);
    }
}
```

### When NOT to Use

❌ **Any domain other than org/provider abilities**

For general caching needs, use:

- `ExtendedCache` for feature-level caching with advanced capabilities
- `IDistributedCache` for simple key-value caching
- Specialized in-memory cache for extreme performance needs

---

## Specialized In-Memory Cache

### When to Use

- **Extremely high-frequency reads** (>10K requests/second)
- **Hot path optimization** where serialization overhead is unacceptable
- **Application-level configuration** that rarely changes
- **Single-instance deployments** or manual cache synchronization is acceptable

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

- Multi-instance deployments requiring consistency (use `ExtendedCache` or `IDistributedCache`)
- Data that benefits from fail-safe mode (use `ExtendedCache`)
- Long-lived OAuth grants (use persistent `IDistributedCache`)

---

## Backend Configuration

### Configuration Priority

The following table shows how different caching options resolve to storage backends based on configuration:

| Cache Option                           | Cloud Backend             | Self-Hosted Backend         | Config Setting                                            |
| -------------------------------------- | ------------------------- | --------------------------- | --------------------------------------------------------- |
| **ExtendedCache**                      | Redis → Memory            | Redis → Memory              | `GlobalSettings.DistributedCache.Redis.ConnectionString`  |
| **IDistributedCache** (default)        | Redis                     | Redis → SQL → EF            | `GlobalSettings.DistributedCache.Redis.ConnectionString`  |
| **IDistributedCache** (`"persistent"`) | Cosmos → Redis            | Redis → SQL → EF            | `GlobalSettings.DistributedCache.Cosmos.ConnectionString` |
| **IApplicationCacheService**           | Memory + Service Bus      | Memory                      | `GlobalSettings.ServiceBus.ConnectionString`              |
| **OAuth Grants** (long-lived)          | Persistent cache (Cosmos) | `IGrantRepository` (SQL/EF) | Various (see above)                                       |

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

### Azure Service Bus (ApplicationCacheService only)

```json
{
  "GlobalSettings": {
    "ServiceBus": {
      "ConnectionString": "Endpoint=sb://...;SharedAccessKeyName=...;SharedAccessKey=...",
      "ApplicationCacheTopicName": "application-cache"
    }
  }
}
```

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
