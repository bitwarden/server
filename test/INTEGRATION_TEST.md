# Integration Testing

How to write integration tests for the Bitwarden server. Assumes familiarity with xUnit and ASP.NET Core's [`WebApplicationFactory<TProgram>`](https://learn.microsoft.com/en-us/dotnet/core/extensions/integration-testing). Database integration tests (repository-layer, cross-provider) are out of scope — see [Infrastructure.IntegrationTest](Infrastructure.IntegrationTest/).

## TL;DR

- **Drive tests through `HttpClient` where you can.** It's the ideal because a purely-HTTP suite can one day run against a real Bitwarden instance for deeper end-to-end validation. When an operation can't be expressed over HTTP — seeding domain state the API doesn't expose, forcing external state for the host to read back, invoking a command directly — an intent method that reaches into DI inside the fixture is fine. Flag those as gaps a future real-instance variant will need to skip.
- **Wrap each project under test in a `FooApplicationFactory` class** that holds a `WebApplicationFactory<Foo.Program>` privately and primarily exposes `HttpClient` accessors plus intent-revealing methods (`RegisterUserAsync`, `LoginAsync`, `ConfirmRegistrationAsync`, …). Tests should call those rather than touching `Services` directly; the intent methods themselves may use DI when there's no HTTP equivalent.
- Integration tests don't need a project of their own — they can live in the matching unit-test project (e.g., a test for `src/Api` goes in `test/Api.Test/`) or in a dedicated `*.IntegrationTest` project.
- The default test database is an **in-memory SQLite connection the application factory owns**.
- Use `IClassFixture<FooApplicationFactory>` (or `IClassFixture<TFixture>` for multi-host) for state isolation. One instance per test class.

The unifying principle: a `FooApplicationFactory` is a small, self-contained abstraction over one host. The same shape should one day be implementable against a real Bitwarden instance, with the in-process variant living in this repo and the real-instance variant doing the same work over HTTP. No shared base, no shared database abstraction.

---

## When to write an integration test

Reach for one when:

- The behavior depends on real DB state or relational invariants the unit layer can't express (cascades, constraints, repository SQL).
- The flow spans multiple controllers, middleware, or the auth pipeline (`/connect/token` → resource endpoint).
- The flow spans multiple hosts (e.g., Admin sends a link the Api consumes).

Anything else — pure logic, validators, single-method behavior — belongs in a unit test with mocked dependencies. Integration tests are slower and noisier; keep them for the seams.

---

## Where the test goes

Integration tests don't need a project of their own — splitting integration from unit by project is a choice, not a requirement. They can live in the matching `*.Test` project (`test/Api.Test/` for `src/Api`, `test/Admin.Test/` for `src/Admin`, etc.) or in a dedicated `*.IntegrationTest` project; pick whichever matches the surface you're working in and what's already there.

When integration tests share a project with unit tests, an `Integration/` subfolder is a nice way to keep them visually separate. The unit-test project may need additional package references it didn't already have (`Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.AspNetCore.TestHost`, `Microsoft.Data.Sqlite`, `Microsoft.EntityFrameworkCore.Sqlite`). Add them to the csproj when you introduce the first integration test.

### Shared infrastructure

[IntegrationTestCommon](IntegrationTestCommon/) (`WebApplicationFactoryBase`, `ITestDatabase`, `SqliteTestDatabase`, etc.) is legacy. New tests should set up their own in-process host inline — see [Authoring an application factory](#authoring-an-application-factory) — rather than take a dependency on it. Existing tests that already use it can keep doing so.

---

## Authoring an application factory

Each project under test gets one `FooApplicationFactory` class (named after the system it wraps — `AdminApplicationFactory`, `ApiApplicationFactory`, etc.). It holds a `WebApplicationFactory<Foo.Program>` configured via `WithWebHostBuilder` — no subclassing needed — and exposes two things to test code:

1. `HttpClient` accessors.
2. Intent-revealing methods (`RegisterUserAsync`, `LoginAsync`, `AssertOrganizationExistsAsync`, …) that describe what the test wants done.

Avoid exposing `Services`, `Server`, or other DI primitives directly to tests. Intent methods may use DI internally — that's the right place for it. Test code should call intent methods rather than reaching for the host's DI itself.

The DB-replacement shape follows Microsoft's [Customize WebApplicationFactory](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests#customize-webapplicationfactory) guidance — remove the host's `IDbContextOptionsConfiguration<TContext>` registration, then call `AddDbContext<TContext>` with an in-memory SQLite connection.

```csharp
public sealed class FooApplicationFactory : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WebApplicationFactory<Foo.Program> _factory;

    public FooApplicationFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _factory = new WebApplicationFactory<Foo.Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                // Dummy SQLite values — the provider has to be set so the host wires
                // up EF Core, but the connection string itself is never used because
                // we replace the DbContext registration in ConfigureServices below.
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["globalSettings:databaseProvider"] = "sqlite",
                    ["globalSettings:sqlite:connectionString"] = "Data Source=ignored.db",
                    ["globalSettings:redis:connectionString"] = "",
                });
            });

            builder.ConfigureServices(services =>
            {
                // Replace EF setup with in-memory SQLite — see Microsoft docs above
                services.RemoveAll<IDbContextOptionsConfiguration<DatabaseContext>>();
                services.AddDbContext<DatabaseContext>(options => options.UseSqlite(_connection));

                // Substitute services here, but only when running the real thing
                // would make the test unachievable. Example: mocking IMailService so
                // a test can capture a verification token from the call args.
                // services.AddSingleton(Substitute.For<IMailService>());
            });
        });

        // Touching Services builds the host; schema then exists for the lifetime
        // of _connection.
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<DatabaseContext>().Database.EnsureCreated();
    }

    public HttpClient CreateClient() => _factory.CreateClient();

    // --- Intent methods ----------------------------------------------------

    public async Task RegisterUserAsync(string email, string password)
    {
        // … HTTP requests to /accounts/register/send-verification-email and
        // /accounts/register/finish, walking the same flow a real client would …
    }

    public async Task<string> LoginAsync(string email, string password)
    {
        // … HTTP request to /connect/token, returns access token …
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        _connection.Dispose();
    }
}
```

Key points:

- **The application factory is the test API.** Test code calls `factory.RegisterUserAsync(...)` rather than `factory.Services.GetRequiredService<...>()`. If you find yourself wanting a generic "give me the DbContext" method, that's a smell — name the intent (`RegisterUserAsync`, `MarkUserAsConfirmedAsync`) instead, and drive it through HTTP when practical. When the operation isn't expressible over HTTP, the intent method can use DI internally — just keep the abstraction at the intent-method boundary, not the DI primitives.
- **`WithWebHostBuilder` returns a configured factory.** Storing it in a field is enough; subclassing `WebApplicationFactory<T>` for tests is rarely necessary.
- **The `SqliteConnection` is the database.** As long as it stays open, the schema and data persist. Close it and the database is gone — that's how isolation works in `IClassFixture<FooApplicationFactory>`.
- **The dummy SQLite config keys are required** because the host's `ConfigureServices` pipeline branches on `globalSettings:databaseProvider` to decide which EF setup to register. Picking `sqlite` lays down EF; we then `RemoveAll` the host's `IDbContextOptionsConfiguration<DatabaseContext>` and re-register with our in-memory connection. The string value is never used.
- **Keep mocking to a minimum.** The point of an integration test is to run real code through real DI. Substitute a service only when the real implementation would make the test unachievable — it would send real emails to real recipients, charge a real card, push real notifications, or require credentials the test environment doesn't have. Don't reflexively mock something just because it's "external."

### Intent methods and the road to real-instance mode

The application-factory shape is designed so that a second implementation can drive the same intent methods against a real Bitwarden cluster over HTTP. When that happens:

- Methods that already use `HttpClient` internally port for free.
- Methods that reach into in-process DI need an HTTP equivalent — or, if there's no way to express the operation against a real instance, the real-instance implementation throws a skip exception so the test reports skipped instead of failed (xUnit v3: `Assert.Skip(...)`; xUnit v2 + `xunit.skippablefact`: `throw new SkipException(...)`).

When writing a new intent method, ask whether the operation is expressible against a real instance. If yes, prefer the HTTP shape even in the in-process variant — it costs nothing now and removes work later. If not — and there are real reasons this happens: state the API doesn't let you set, third-party state the host reads back from, a command the user surface doesn't reach — reach into DI inside the intent method and leave a short comment so the gap is visible when the real-instance variant is built. A DI-backed intent method is better than a leaked `Services` accessor.

---

## Writing tests

**Group tests by the scenario being exercised, not by the controller or class being called.** A test class describes a behavior (`RetrievingFooTests`, `UserRegistrationFlowTests`, `BusinessUnitConversionTests`), and its methods are variations on that scenario (happy path, edge cases, failure modes). Mirroring source file structure (`FooControllerTests`) bakes in production-class coupling that's irrelevant to the behavior under test and obscures what's actually being verified.

Use `IClassFixture<FooApplicationFactory>` so the application factory (and its in-memory database) is constructed once per class, then disposed at the end. Tests interact through `CreateClient()` and the intent methods — never through `Services` or any host primitive:

```csharp
public class RetrievingFooTests(FooApplicationFactory factory) : IClassFixture<FooApplicationFactory>
{
    [Fact]
    public async Task AuthenticatedUser_GetsFoo_ReturnsOk()
    {
        await factory.RegisterUserAsync("test@example.com", "password");
        var token = await factory.LoginAsync("test@example.com", "password");

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/foo");
        await Assert.SuccessResponseAsync(response);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
    }
}
```

The test body reads as intent — "register a user, log in, retrieve foo, assert." Re-pointed at a real Bitwarden instance, every line still makes sense. That's the bar.

### Request and response models

The example uses an anonymous object for the request body and `JsonObject` for the response. The alternative is the production DTOs (`OrganizationCreateRequest`, etc.) directly:

- **Production DTOs**: typed access, IDE rename, less verbose — especially for deep responses. A wire-shape change made alongside an `[JsonPropertyName]` swap still compiles and passes, so old clients pinned to the previous shape can break silently.
- **Anonymous objects + `JsonObject`**: wire-shape drift breaks the test, surfacing breakage for old clients. More verbose, especially when reading deep responses.

### Asserting HTTP responses

`HttpResponseMessage.EnsureSuccessStatusCode()` throws with just the status code — no body, no clue what actually went wrong. Use [`Assert.SuccessResponseAsync(response)`](Common/Helpers/AssertExtensions.cs) instead. It's a C# 14 extension on `Assert` that slots into the existing xUnit vocabulary (`Assert.Equal`, `Assert.NotNull`, `Assert.SuccessResponseAsync`) and surfaces the response body — pretty-printed when it's JSON — in the failure message:

```csharp
var response = await client.GetAsync("/foo");
await Assert.SuccessResponseAsync(response);
```

Add a `<ProjectReference>` to `test/Common/Common.csproj` (`Bit.Test.Common`) when your unit-test project doesn't already have one.

---

## Multi-app fixtures

When one host produces a side effect another host consumes — Admin sends a link the Api side later validates, or an Admin-issued conversion token the Api redeems — compose application factories in a fixture. The fixture owns any shared in-process state (e.g., a shared SQLite connection) and exposes the factories to tests.

```csharp
public sealed class FooBarFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    public FooApplicationFactory Foo { get; }
    public BarApplicationFactory Bar { get; }

    public FooBarFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Internal constructors let the fixture inject the shared connection;
        // test code can't reach them.
        Foo = new FooApplicationFactory(_connection, owns: true);
        Bar = new BarApplicationFactory(_connection, owns: false);
    }

    public async ValueTask DisposeAsync()
    {
        await Foo.DisposeAsync();
        await Bar.DisposeAsync();
        _connection.Dispose();
    }
}
```

Rules:

1. **The fixture owns the shared state.** A shared `SqliteConnection` lives on the fixture, not on either application factory. Each factory takes it via an `internal` constructor (or method) so test code can't reach it.
2. **Only the primary factory runs `EnsureCreated`** (controlled by the `owns: true` flag in the example). Secondaries register `UseSqlite(sharedConnection)` against the same instance and skip schema creation.
3. **For side-effect capture (e.g., extracting a token from a mocked `IMailService` send)**, do it inside the application factory as an intent method (`factory.NextSentLinkTokenAsync()` or similar). Don't expose `ConcurrentDictionary<…>` on the fixture for tests to poke.
4. **`IClassFixture<TFixture>` binds the composition to the test class.** xUnit constructs the fixture once for the class, sharing both factories across every test.

For an example of a working multi-app composition (in legacy shape — useful as a reference for the wiring, not the abstraction), see [Billing.IntegrationTest/StripeTestsFixture.cs](Billing.IntegrationTest/StripeTestsFixture.cs) and [AdminApplicationFactory.cs](Billing.IntegrationTest/AdminApplicationFactory.cs).

---

## Anti-patterns

- **Exposing `Services`, `Server`, or `DatabaseContext` on the application factory.** Bypasses the abstraction and couples the test to the in-process host. Add an intent method that names what the test wants done — even if it uses DI internally.
- **Tests touching `factory.Services` or building their own `WebApplicationFactory<T>`.** The test ends up depending on the host shape rather than the application factory's API. Route through an intent method on the factory instead — wrapping a DI call in a named method is fine; calling DI directly from the test isn't.
- **Inheriting from `WebApplicationFactoryBase` or using `ITestDatabase`/`SqliteTestDatabase`.** Both belong to the legacy shared infrastructure. Use `WebApplicationFactory<TProgram>` plus `WithWebHostBuilder` directly and set up SQLite inline.
- **Mutating application-factory state after the host has been built.** `WithWebHostBuilder` is consumed once when the host is created (first `Services` access). Late mutations silently no-op and the bug looks like "my override didn't apply." Set everything in the constructor or fixture.
- **Sharing one application factory across tests that mutate state, without an `IClassFixture` boundary.** State bleeds between tests in unpredictable order. Either use `IClassFixture<FooApplicationFactory>` per class, or expose a reset intent method and call it from `IAsyncLifetime.InitializeAsync`.
- **Closing the SQLite connection before the application factory disposes.** Closing the connection drops the in-memory database immediately; any in-flight request fails. Keep it open for the application factory's full lifetime and let `DisposeAsync` close it.
- **Naming test classes after controllers** (`FooControllerTests`). Name them after the scenario being tested (`RetrievingFooTests`, `UserRegistrationFlowTests`). Production-class mirroring obscures what behavior the class actually exercises.
- **Calling `EnsureSuccessStatusCode()`.** The error message contains only the status code — no body, no diagnostic. Use `Assert.SuccessResponseAsync(response)` instead; it includes the (pretty-printed JSON) response body in the failure message so the test tells you what actually broke.
