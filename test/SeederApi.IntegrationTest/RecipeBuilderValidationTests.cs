using Bit.Seeder.Pipeline;
using Xunit;

namespace Bit.SeederApi.IntegrationTest;

public class RecipeBuilderValidationTests
{
    [Fact]
    public void UseRoster_AfterAddUsers_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new RecipeBuilder();

        // Act & Assert
        builder.AddUsers(10);
        var ex = Assert.Throws<InvalidOperationException>(() => builder.UseRoster("test"));
        Assert.Contains("Cannot call UseRoster() after AddUsers()", ex.Message);
    }

    [Fact]
    public void AddUsers_AfterUseRoster_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new RecipeBuilder();

        // Act & Assert
        builder.UseRoster("test");
        var ex = Assert.Throws<InvalidOperationException>(() => builder.AddUsers(10));
        Assert.Contains("Cannot call AddUsers() after UseRoster()", ex.Message);
    }

    [Fact]
    public void UseCiphers_AfterAddCiphers_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new RecipeBuilder();

        // Act & Assert
        builder.AddCiphers(10);
        var ex = Assert.Throws<InvalidOperationException>(() => builder.UseCiphers("test"));
        Assert.Contains("Cannot call UseCiphers() after AddCiphers()", ex.Message);
    }

    [Fact]
    public void AddCiphers_AfterUseCiphers_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new RecipeBuilder();

        // Act & Assert
        builder.UseCiphers("test");
        var ex = Assert.Throws<InvalidOperationException>(() => builder.AddCiphers(10));
        Assert.Contains("Cannot call AddCiphers() after UseCiphers()", ex.Message);
    }

    [Fact]
    public void AddGroups_WithoutUsers_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new RecipeBuilder();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => builder.AddGroups(5));
        Assert.Contains("Groups require users", ex.Message);
    }

    [Fact]
    public void AddCollections_WithoutUsers_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new RecipeBuilder();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => builder.AddCollections(5));
        Assert.Contains("Collections require users", ex.Message);
    }

    [Fact]
    public void AddGroups_AfterAddUsers_Succeeds()
    {
        // Arrange
        var builder = new RecipeBuilder();

        // Act & Assert - Should not throw
        builder.AddUsers(10);
        builder.AddGroups(5);
    }

    [Fact]
    public void AddCollections_AfterUseRoster_Succeeds()
    {
        // Arrange
        var builder = new RecipeBuilder();

        // Act & Assert - Should not throw
        builder.UseRoster("test");
        builder.AddCollections(5);
    }

    [Fact]
    public void Build_WithoutOrg_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new RecipeBuilder();

        // Act & Assert
        builder.AddOwner();
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("Organization is required", ex.Message);
    }

    [Fact]
    public void Build_WithoutOwner_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new RecipeBuilder();

        // Act & Assert
        builder.UseOrganization("test");
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("Owner is required", ex.Message);
    }

    [Fact]
    public void Build_AddCiphersWithoutGenerator_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new RecipeBuilder();

        // Act & Assert
        builder.UseOrganization("test");
        builder.AddOwner();
        builder.AddUsers(10);
        builder.AddCiphers(50);
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("Generated ciphers require a generator", ex.Message);
    }
}
