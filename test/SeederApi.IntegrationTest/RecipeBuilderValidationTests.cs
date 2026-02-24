using Bit.Seeder;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
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
        Assert.Contains("Organization is required", ex.Message);
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
        var steps = provider.GetKeyedServices<IStep>("test").ToList();

        Assert.Equal(7, steps.Count);

        // Verify steps are wrapped in OrderedStep with sequential order values
        var orderedSteps = steps.Cast<OrderedStep>().ToList();
        for (var i = 0; i < orderedSteps.Count; i++)
        {
            Assert.Equal(i, orderedSteps[i].Order);
        }
    }

    private static readonly ISeedReader _stubReader = new StubSeedReader(hasOwner: false);
    private static readonly ISeedReader _stubReaderWithOwner = new StubSeedReader(hasOwner: true);

    /// <summary>
    /// Stub reader for builder validation tests that don't need real fixture data.
    /// </summary>
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
    }
}
