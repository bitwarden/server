## Extended Cache

`ExtendedCache` is a wrapper around [FusionCache](https://github.com/ZiggyCreatures/FusionCache)
that provides a simple way to register **named, isolated caches** with sensible defaults.
The goal is to make it trivial for each subsystem or feature to have its own cache -
with optional distributed caching and backplane support - without repeatedly wiring up
FusionCache, Redis, and related infrastructure.

Each named cache automatically receives:

- Its own `FusionCache` instance
- Its own configuration (default or overridden)
- Its own key prefix
- Optional distributed store
- Optional backplane

`ExtendedCache` supports several deployment modes:

- **Memory-only caching** (with stampede protection)
- **Memory + distributed cache + backplane** using the **shared** application Redis
- **Memory + distributed cache + backplane** using a **fully isolated** Redis instance

**Note**: When using the shared Redis cache option (which is on by default, if the
Redis connection string is configured), it is expected to call
`services.AddDistributedCache(globalSettings)` **before** calling
`AddExtendedCache`. The idea is to set up the distributed cache in our normal pattern
and then "extend" it to include more functionality.

### Configuration

`ExtendedCache` exposes a set of default properties that define how each named cache behaves.
These map directly to FusionCache configuration options such as timeouts, duration,
jitter, fail-safe mode, etc. Any cache can override these defaults independently.

#### Default configuration

The simplest approach registers a new named cache with default settings and reusing
the existing distributed cache:

``` csharp
    services.AddDistributedCache(globalSettings);
    services.AddExtendedCache(cacheName, globalSettings);
```

By default:
 - If `GlobalSettings.DistributedCache.Redis.ConnectionString` is configured:
   - The cache is memory + distributed (Redis)
   - The Redis cache created by `AddDistributedCache` is re-used
   - A Redis backplane is configured, re-using the same multiplexer
 - If Redis is **not** configured the cache automatically falls back to memory-only

#### Overriding default properties

A number of default properties are provided (see
`GlobalSettings.DistributedCache.DefaultExtendedCache` for specific values). A named
cache can override any (or all) of these properties simply by providing its own
instance of `ExtendedCacheSettings`:

``` csharp
    services.AddExtendedCache(cacheName, globalSettings, new GlobalSettings.ExtendedCacheSettings
        {
            Duration = TimeSpan.FromHours(1),
        });
```

This example keeps all other defaults—including shared Redis—but changes the
default cached item duration from 30 minutes to 1 hour.

#### Isolated Redis configuration

ExtendedCache can also run in a fully isolated mode where the cache uses its own:
 - Redis multiplexer
 - Distributed cache
 - Backplane

To enable this, specify a Redis connection string and set `UseSharedRedisCache`
to `false`:

``` csharp
    services.AddExtendedCache(cacheName, globalSettings, new GlobalSettings.ExtendedCacheSettings
        {
            UseSharedRedisCache = false,
            Redis = new GlobalSettings.ConnectionStringSettings { ConnectionString = "localhost:6379" }
        });
```

When configured this way:
 - A dedicated `IConnectionMultiplexer` is created
 - A dedicated `IDistributedCache` is created
 - A dedicated FusionCache backplane is created
 - All three are exposed to DI as keyed services (using the cache name as service key)

### Accessing a named cache

A named cache can be retrieved either:
 - Directly via DI using keyed services
 - Through `IFusionCacheProvider` (similar to
   [IHttpClientFactory](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-7.0#named-clients))

#### Keyed service

In the consuming class, declare an IFusionCache field:

```csharp
    private IFusionCache _cache;
```

Then ask DI to inject the keyed cache:

```csharp
    public MyService([FromKeyedServices("MyCache")] IFusionCache cache)
    {
        _cache = cache;
    }
```

Or request it manually:

```csharp
    cache: provider.GetRequiredKeyedService<IFusionCache>(serviceKey: cacheName)
```

#### Injecting a provider

Alternatively, an `IFusionCacheProvider` can be injected and used to request a named
cache - similar to how `IHttpClientFactory` can be used to create named `HttpClient`
instances

In the class using the cache, use an injected provider to request the named cache:

```csharp
    private readonly IFusionCache _cache;

    public MyController(IFusionCacheProvider cacheProvider)
    {
        _cache = cacheProvider.GetCache("CacheName");
    }
```

### Using a cache

Using the cache in code is as simple as replacing the direct repository calls with
`FusionCache`'s `GetOrSet` call. If the class previously fetched an `Item` from
an `ItemRepository`, all that we need to do is provide a key and the original
repository call as the fallback:

```csharp
        var item = _cache.GetOrSet<Item>(
            $"item:{id}",
            _ => _itemRepository.GetById(id)
        );
```

`ExtendedCache` doesn’t change how `FusionCache` is used in code, which means all
the functionality and full `FusionCache` API is available. See the
[FusionCache docs](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/CoreMethods.md)
for more details.
