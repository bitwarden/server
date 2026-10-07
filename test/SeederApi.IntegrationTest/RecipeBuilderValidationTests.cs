using Bit.Core.Billing.Services;
using Bit.Seeder;
using Bit.Seeder.Models;
using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Bit.Seeder.Steps;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public class RecipeBuilderValidationTests
{
    [Fact]
    public void UseRoster_AfterAddUsers_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        builder.AddUsers(10);
        var ex = Assert.Throws<InvalidOperationException>(() => builder.UseRoster("test", _stubReader));
        Assert.Contains("Cannot call UseRoster() after AddUsers()", ex.Message);
    }

    [Fact]
    public void AddUsers_AfterUseRoster_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        builder.UseRoster("test", _stubReader);
        var ex = Assert.Throws<InvalidOperationException>(() => builder.AddUsers(10));
        Assert.Contains("Cannot call AddUsers() after UseRoster()", ex.Message);
    }

    [Fact]
    public void UseCiphers_AfterAddCiphers_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        builder.AddCiphers(10);
        var ex = Assert.Throws<InvalidOperationException>(() => builder.UseCiphers("test"));
        Assert.Contains("Cannot call UseCiphers() after AddCiphers()", ex.Message);
    }

    [Fact]
    public void AddCiphers_AfterUseCiphers_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        builder.UseCiphers("test");
        var ex = Assert.Throws<InvalidOperationException>(() => builder.AddCiphers(10));
        Assert.Contains("Cannot call AddCiphers() after UseCiphers()", ex.Message);
    }

    [Fact]
    public void AddGroups_WithoutUsers_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.AddGroups(5));
        Assert.Contains("Groups require users", ex.Message);
    }

    [Fact]
    public void AddCollections_WithoutUsers_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.AddCollections(5));
        Assert.Contains("Collections require users", ex.Message);
    }

    [Fact]
    public void AddGroups_AfterAddUsers_Succeeds()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        builder.AddUsers(10);
        builder.AddGroups(5);
    }

    [Fact]
    public void AddCollections_AfterUseRoster_Succeeds()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        builder.UseRoster("test", _stubReader);
        builder.AddCollections(5);
    }

    [Fact]
    public void Validate_WithoutOrg_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        builder.AddOwner();
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Validate());
        Assert.Contains("Organization or individual user is required", ex.Message);
    }

    [Fact]
    public void Validate_WithoutOwner_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        builder.UseOrganization("test");
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Validate());
        Assert.Contains("Owner is required", ex.Message);
    }

    [Fact]
    public void Validate_WithRosterOwner_Succeeds()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        builder.UseOrganization("test");
        builder.UseRoster("test", _stubReaderWithOwner);

        builder.Validate(); // should not throw — roster provides the owner
    }

    [Fact]
    public void Validate_AddCiphersWithoutGenerator_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        builder.UseOrganization("test");
        builder.AddOwner();
        builder.AddUsers(10);
        builder.AddCiphers(50);
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Validate());
        Assert.Contains("Generated ciphers require a generator", ex.Message);
    }

    [Fact]
    public void Validate_BillingWithoutOrganization_Throws()
    {
        // The billing step reads context.Organization, so an individual-user recipe could only
        // fail at run time — after the commit — without this guard.
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        builder.CreateIndividualUser("solo@bw.example", premium: true, maxStorageGb: 1);
        builder.WithStripeBilling(new StripeBillingOptions());

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Validate());
        Assert.Contains("Stripe billing requires an organization", ex.Message);
    }

    [Fact]
    public void Validate_BillingWithOrganization_Succeeds()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        builder.UseOrganization("test");
        builder.AddOwner();
        builder.WithStripeBilling(new StripeBillingOptions());

        builder.Validate();
    }

    [Fact]
    public void StepsExecuteInRegistrationOrder()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        builder.UseOrganization("test-org");
        builder.AddOwner();
        builder.WithGenerator("test.com");
        builder.AddUsers(5);
        builder.AddGroups(2);
        builder.AddCollections(3);
        builder.AddCiphers(10);

        builder.Validate();

        using var provider = services.BuildServiceProvider();
        var steps = provider.GetKeyedServices<OrderedStep>("test").ToList();

        Assert.Equal(7, steps.Count);

        // Verify steps are wrapped in OrderedStep with sequential order values
        for (var i = 0; i < steps.Count; i++)
        {
            Assert.Equal(i, steps[i].Order);
        }
    }

    [Fact]
    public void CreateIndividualUser_ProducesTwoStepsInOrder()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        builder.CreateIndividualUser("user@example.com", true, 1, true);
        services.AddLogging();
        services.AddSingleton<ILicensingService>(
            new LicenseTestHelpers.StubLicensingService((_, _) => Task.CompletedTask));
        services.AddSingleton<ISeederLicenseSigner>(
            new LicenseTestHelpers.StubSeederLicenseSigner(
                _ => Task.FromResult(LicenseSigningResult.Skipped("no signing certificate configured"))));

        using var provider = services.BuildServiceProvider();
        var steps = provider.GetKeyedServices<OrderedStep>("test")
            .OrderBy(s => s.Order)
            .ToList();

        Assert.Equal(2, steps.Count);
        // First step must be the user creation step; second must be the license step.
        // If this order is reversed, GenerateSelfHostUserLicenseStep reads a null context.Owner.
        var inner0 = steps[0].Inner;
        var inner1 = steps[1].Inner;
        Assert.IsType<CreateIndividualUserStep>(inner0);
        Assert.IsType<GenerateSelfHostUserLicenseStep>(inner1);
    }

    [Fact]
    public void AddStep_SyncPostCommitMarker_SetsIsPostCommit()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        builder.AddStep(_ => new PostCommitSyncStub());
        builder.AddStep(_ => new SyncStub());

        using var provider = services.BuildServiceProvider();
        var steps = provider.GetKeyedServices<OrderedStep>("test").OrderBy(s => s.Order).ToList();

        Assert.True(steps[0].IsPostCommit);
        Assert.False(steps[1].IsPostCommit);
    }

    [Fact]
    public void AddAsyncStep_PostCommitMarker_SetsIsPostCommit()
    {
        var services = new ServiceCollection();
        var builder = services.AddRecipe("test");

        builder.AddAsyncStep(_ => new PostCommitAsyncStub());
        builder.AddAsyncStep(_ => new AsyncStub());

        using var provider = services.BuildServiceProvider();
        var steps = provider.GetKeyedServices<OrderedStep>("test").OrderBy(s => s.Order).ToList();

        Assert.True(steps[0].IsPostCommit);
        Assert.False(steps[1].IsPostCommit);
    }

    private sealed class SyncStub : IStep
    {
        public void Execute(SeederContext context) { }
    }

    private sealed class PostCommitSyncStub : IStep, IPostCommitStep
    {
        public void Execute(SeederContext context) { }
    }

    private sealed class AsyncStub : IAsyncStep
    {
        public Task ExecuteAsync(SeederContext context) => Task.CompletedTask;
    }

    private sealed class PostCommitAsyncStub : IAsyncStep, IPostCommitStep
    {
        public Task ExecuteAsync(SeederContext context) => Task.CompletedTask;
    }

    private static readonly ISeedReader _stubReader = new StubSeedReader(hasOwner: false);
    private static readonly ISeedReader _stubReaderWithOwner = new StubSeedReader(hasOwner: true);

    private sealed class StubSeedReader(bool hasOwner) : ISeedReader
    {
        public T Read<T>(string seedName) =>
            (T)(object)new SeedRoster
            {
                Users = [new SeedRosterUser
                {
                    FirstName = "Test",
                    LastName = "User",
                    Role = hasOwner ? "owner" : "user"
                }]
            };

        public IReadOnlyList<string> ListAvailable() => [];

        public byte[] ReadBytes(string fileName) =>
            throw new NotSupportedException("StubSeedReader does not provide binary samples.");
    }
}
