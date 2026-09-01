using AutoMapper;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder;
using Bit.Seeder.Pipeline;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Pipeline;

/// <summary>
/// Verifies the pre-commit / post-commit partition in <see cref="RecipeExecutor"/> against a real
/// (in-memory SQLite) database. Row counts are the only signal that proves the commit actually ran —
/// cleared entity lists prove nothing, because <c>BulkCommitter</c> early-returns on empty lists.
/// </summary>
public sealed class RecipeExecutorPostCommitTests : IDisposable
{
    private const string _recipe = "post-commit-test";

    private readonly SqliteConnection _connection;
    private readonly ServiceCollection _services = new();
    private readonly List<Observation> _log = [];
    private readonly List<SeederProgressEvent> _events = [];

    private ServiceProvider? _provider;

    public RecipeExecutorPostCommitTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // DatabaseContext.OnModelCreating resolves IDataProtectionProvider for the
        // User.Key / User.MasterPassword field converters, so DI must include it.
        _services.AddLogging();
        _services.AddDataProtection();
        _services.AddDbContext<DatabaseContext>(opts => opts.UseSqlite(_connection));
        _services.AddAutoMapper(typeof(UserRepository));
        _services.AddSingleton(new SeederSettings());
        _services.AddSingleton<IProgress<SeederProgressEvent>>(new RecordingProgress(_events));
    }

    public void Dispose()
    {
        _provider?.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task PostCommitStep_RunsAfterCommit_AndSeesCommittedRowAsync()
    {
        var owner = NewUser();
        var builder = _services.AddRecipe(_recipe);
        builder.AddStep(_ => new RecordingStep("pre", _log, StageOwner(owner)));
        builder.AddAsyncStep(_ => new RecordingAsyncPostCommitStep("post", _log));

        await BuildExecutor().ExecuteAsync();

        Assert.Equal(["pre", "post"], _log.Select(o => o.Label));

        var pre = _log[0];
        var post = _log[1];

        // The observation that list-clearing cannot fake: the row is only queryable after the commit.
        Assert.Equal(0, pre.UsersInDb);
        Assert.Equal(1, post.UsersInDb);

        // BulkCommitter clears the entity lists, but the registry and the context scalars survive.
        Assert.Equal(1, pre.UsersInContext);
        Assert.Equal(0, post.UsersInContext);
        Assert.Equal(1, post.UserDigestsInRegistry);
        Assert.Equal(owner.Id, post.OwnerId);
    }

    [Fact]
    public async Task PostCommitStep_CannotContributeToResultAsync()
    {
        // Pins the snapshot semantics documented on RecipeExecutor.ExecuteAsync: the result is captured
        // before the commit, so anything a post-commit step adds to the context is invisible to it
        // (and, because the commit already ran, never reaches the database either).
        var builder = _services.AddRecipe(_recipe);
        builder.AddStep(_ => new RecordingStep("pre", _log, StageOwner(NewUser())));
        builder.AddAsyncStep(_ => new StagingPostCommitStep(NewUser()));

        var result = await BuildExecutor().ExecuteAsync();

        Assert.Equal(1, result.UsersCount);
        Assert.Equal(1, UsersInDb());
    }

    [Fact]
    public async Task PostCommitStep_GatewayIdsOnOrganization_ReachTheResultAsync()
    {
        // The one documented exception to "a post-commit step cannot contribute to the result":
        // RecipeExecutor re-projects the organization's gateway IDs after the post-commit loop.
        var builder = _services.AddRecipe(_recipe);
        builder.AddStep(_ => new RecordingStep("pre", _log, context =>
        {
            StageOwner(NewUser())(context);
            context.Organization = new Organization { Id = CombGuid.Generate() };
        }));
        builder.AddAsyncStep(_ => new GatewayStampingPostCommitStep("cus_seeded", "sub_seeded"));

        var result = await BuildExecutor().ExecuteAsync();

        Assert.Equal("cus_seeded", result.GatewayCustomerId);
        Assert.Equal("sub_seeded", result.GatewaySubscriptionId);
    }

    [Fact]
    public async Task NoBillingStep_LeavesGatewayIdsNullAsync()
    {
        var builder = _services.AddRecipe(_recipe);
        builder.AddStep(_ => new RecordingStep("pre", _log, StageOwner(NewUser())));

        var result = await BuildExecutor().ExecuteAsync();

        Assert.Null(result.GatewayCustomerId);
        Assert.Null(result.GatewaySubscriptionId);
    }

    [Fact]
    public async Task PostCommitStep_IsActuallyAwaitedAsync()
    {
        // Fire-and-forget guard, with no dependence on scheduling luck. The step blocks on a gate
        // nobody has completed yet, so an executor that awaits it *cannot* have finished: its task is
        // observably incomplete. An executor that dropped the await would run the loop to the end and
        // hand back an already-completed task, failing the first assert.
        var gate = new TaskCompletionSource();
        var builder = _services.AddRecipe(_recipe);
        builder.AddStep(_ => new RecordingStep("pre", _log, StageOwner(NewUser())));
        builder.AddAsyncStep(_ => new GatedPostCommitStep("post", _log, gate.Task));

        var execution = BuildExecutor().ExecuteAsync();

        Assert.False(execution.IsCompleted);
        Assert.Equal(["pre"], _log.Select(o => o.Label));

        gate.SetResult();
        await execution;

        Assert.Equal(["pre", "post"], _log.Select(o => o.Label));
    }

    [Fact]
    public async Task PreCommitStep_IsActuallyAwaited_AndBlocksTheCommit()
    {
        // Same gate on the higher-consequence side. Dropping the pre-commit await would let
        // BulkCommitter.Commit run while an async step was still mutating the context, committing
        // partial data — and every other test here would still pass. Asserting an empty table while
        // the step is parked is what pins it: the commit provably has not run yet.
        var gate = new TaskCompletionSource();
        var builder = _services.AddRecipe(_recipe);
        builder.AddAsyncStep(_ => new GatedPreCommitStep("pre", _log, gate.Task, StageOwner(NewUser())));

        var execution = BuildExecutor().ExecuteAsync();

        Assert.False(execution.IsCompleted);
        Assert.Empty(_log);
        Assert.Equal(0, UsersInDb());

        gate.SetResult();
        await execution;

        Assert.Equal(["pre"], _log.Select(o => o.Label));
        Assert.Equal(1, UsersInDb());
    }

    [Fact]
    public async Task NoPostCommitSteps_EmitsNoPostCommitPhaseAsync()
    {
        // The zero-behavior-change guard: without a post-commit step the progress stream must match
        // the pre-refactor sequence exactly, or every CLI run gains a visible progress bar.
        var builder = _services.AddRecipe(_recipe);
        builder.AddStep(_ => new RecordingStep("pre", _log, StageOwner(NewUser())));

        await BuildExecutor().ExecuteAsync();

        Assert.Collection(_events,
            e => Assert.Equal(new PhaseStarted(SeederPhases.CommittingToDatabase, null), e),
            e => Assert.Equal(new PhaseCompleted(SeederPhases.CommittingToDatabase), e));
    }

    [Fact]
    public async Task PostCommitStep_RegisteredFirst_StillRunsLastAsync()
    {
        // Proves the partition beats the order index rather than registration order happening to match.
        // Uses the *synchronous* IStep + IPostCommitStep pairing, so the suite covers deferral through
        // both step interfaces end-to-end (the other tests here defer an IAsyncStep).
        var builder = _services.AddRecipe(_recipe);
        builder.AddStep(_ => new RecordingSyncPostCommitStep("post", _log));
        builder.AddStep(_ => new RecordingStep("pre-a", _log, StageOwner(NewUser())));
        builder.AddStep(_ => new RecordingStep("pre-b", _log));

        await BuildExecutor().ExecuteAsync();

        Assert.Equal(["pre-a", "pre-b", "post"], _log.Select(o => o.Label));
        Assert.Equal(1, _log[^1].UsersInDb);
    }

    /// <summary>
    /// Builds the container, creates the schema, and wires an executor over the real
    /// <see cref="BulkCommitter"/>. Call once per test, after every step is registered.
    /// </summary>
    private RecipeExecutor BuildExecutor()
    {
        _provider = _services.BuildServiceProvider();
        _provider.GetRequiredService<DatabaseContext>().Database.EnsureCreated();

        var committer = new BulkCommitter(
            _provider.GetRequiredService<DatabaseContext>(),
            _provider.GetRequiredService<IMapper>());

        return new RecipeExecutor(_recipe, _provider, committer);
    }

    private int UsersInDb() => _provider!.GetRequiredService<DatabaseContext>().Users.Count();

    /// <summary>
    /// Stages a user for the commit the way a real step would: on the context's entity list, on the
    /// registry, and as the context owner.
    /// </summary>
    private static Action<SeederContext> StageOwner(User user) => context =>
    {
        context.Users.Add(user);
        context.Registry.UserDigests.Add(new EntityRegistry.UserDigest(user.Id, Guid.Empty, "unused-vault-key"));
        context.Owner = user;
    };

    private static User NewUser() => new()
    {
        Id = CombGuid.Generate(),
        Email = $"post-commit-{Guid.NewGuid():N}@bw.example",
        SecurityStamp = Guid.NewGuid().ToString(),
        ApiKey = "test-api-key",
    };

    /// <summary>What a step could see at the moment it ran.</summary>
    private sealed record Observation(
        string Label,
        int UsersInDb,
        int UsersInContext,
        int UserDigestsInRegistry,
        Guid? OwnerId)
    {
        internal static Observation Capture(string label, SeederContext context) =>
            new(label,
                context.Services.GetRequiredService<DatabaseContext>().Users.Count(),
                context.Users.Count,
                context.Registry.UserDigests.Count,
                context.Owner?.Id);
    }

    private sealed class RecordingProgress(List<SeederProgressEvent> events) : IProgress<SeederProgressEvent>
    {
        public void Report(SeederProgressEvent value) => events.Add(value);
    }

    private sealed class RecordingStep(string label, List<Observation> log, Action<SeederContext>? stage = null) : IStep
    {
        public void Execute(SeederContext context)
        {
            stage?.Invoke(context);
            log.Add(Observation.Capture(label, context));
        }
    }

    private sealed class RecordingSyncPostCommitStep(string label, List<Observation> log) : IStep, IPostCommitStep
    {
        public void Execute(SeederContext context) => log.Add(Observation.Capture(label, context));
    }

    /// <summary>Deliberately not <c>async</c>: CS1998 is a build error under TreatWarningsAsErrors.</summary>
    private sealed class RecordingAsyncPostCommitStep(string label, List<Observation> log) : IAsyncStep, IPostCommitStep
    {
        public Task ExecuteAsync(SeederContext context)
        {
            log.Add(Observation.Capture(label, context));
            return Task.CompletedTask;
        }
    }

    /// <summary>Stands in for <c>FinalizeOrganizationBillingStep</c>: stamps gateway IDs onto the committed org.</summary>
    private sealed class GatewayStampingPostCommitStep(string customerId, string subscriptionId)
        : IAsyncStep, IPostCommitStep
    {
        public Task ExecuteAsync(SeederContext context)
        {
            var organization = context.RequireOrganization();
            organization.GatewayCustomerId = customerId;
            organization.GatewaySubscriptionId = subscriptionId;
            return Task.CompletedTask;
        }
    }

    private sealed class StagingPostCommitStep(User user) : IAsyncStep, IPostCommitStep
    {
        public Task ExecuteAsync(SeederContext context)
        {
            context.Users.Add(user);
            return Task.CompletedTask;
        }
    }

    /// <summary>Blocks on <paramref name="gate"/> so the executor's own task stays observably incomplete.</summary>
    private sealed class GatedPostCommitStep(string label, List<Observation> log, Task gate) : IAsyncStep, IPostCommitStep
    {
        public async Task ExecuteAsync(SeederContext context)
        {
            await gate;
            log.Add(Observation.Capture(label, context));
        }
    }

    /// <summary>
    /// Pre-commit twin of <see cref="GatedPostCommitStep"/> — no <see cref="IPostCommitStep"/> marker.
    /// Stages its entity only after the gate opens, so a dropped await commits an empty context.
    /// </summary>
    private sealed class GatedPreCommitStep(
        string label,
        List<Observation> log,
        Task gate,
        Action<SeederContext> stage) : IAsyncStep
    {
        public async Task ExecuteAsync(SeederContext context)
        {
            await gate;
            stage(context);
            log.Add(Observation.Capture(label, context));
        }
    }
}
