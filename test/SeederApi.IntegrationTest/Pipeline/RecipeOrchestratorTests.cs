using Bit.Seeder.Data.Enums;
using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Steps;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Pipeline;

public class RecipeOrchestratorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureOwnerEmailUnique_NullOrWhitespaceOverride_DoesNotInvokePredicate(string? override_)
    {
        var predicateCalled = false;
        RecipeOrchestrator.EnsureOwnerEmailUnique(
            override_,
            manglingEnabled: false,
            email =>
            {
                predicateCalled = true;
                return true;
            });

        Assert.False(predicateCalled);
    }

    [Fact]
    public void EnsureOwnerEmailUnique_ManglingEnabled_SkipsCheck()
    {
        // When mangling is on, the per-run unique tag prevents collisions, so the
        // pre-flight check is unnecessary even if the override email exists.
        var predicateCalled = false;

        RecipeOrchestrator.EnsureOwnerEmailUnique(
            "would-collide@bw.example",
            manglingEnabled: true,
            email =>
            {
                predicateCalled = true;
                return true;
            });

        Assert.False(predicateCalled);
    }

    [Fact]
    public void EnsureOwnerEmailUnique_NoCollision_DoesNotThrow()
    {
        RecipeOrchestrator.EnsureOwnerEmailUnique(
            "fresh@bw.example",
            manglingEnabled: false,
            email => false);
    }

    [Fact]
    public void EnsureOwnerEmailUnique_Collision_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            RecipeOrchestrator.EnsureOwnerEmailUnique(
                "existing@bw.example",
                manglingEnabled: false,
                email => true));

        Assert.Contains("existing@bw.example", ex.Message);
        Assert.Contains("--mangle", ex.Message);
    }

    [Fact]
    public void EnsureOwnerEmailUnique_PassesOverrideEmailToPredicate()
    {
        string? capturedEmail = null;

        RecipeOrchestrator.EnsureOwnerEmailUnique(
            "specific@bw.example",
            manglingEnabled: false,
            email =>
            {
                capturedEmail = email;
                return false;
            });

        Assert.Equal("specific@bw.example", capturedEmail);
    }

    [Fact]
    public void RegisterRecipe_WithRegion_GeneratorUsesRegion()
    {
        var services = new ServiceCollection();
        var options = new OrganizationVaultOptions
        {
            Name = "Region Org",
            Domain = "region.example",
            Users = 1,
            Region = GeographicRegion.Europe
        };

        var recipeName = RecipeOrchestrator.RegisterRecipe(options, services);

        using var provider = services.BuildServiceProvider();
        var initGenerator = provider.GetKeyedServices<OrderedStep>(recipeName)
            .Select(s => s.Inner)
            .OfType<InitGeneratorStep>()
            .Single();
        var context = new SeederContext(provider);
        initGenerator.Execute(context);

        Assert.Equal("AB 10 11 12 C", context.RequireGenerator().Identity.GenerateByIndex(0).SSN);
    }
}
