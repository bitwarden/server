using Microsoft.Extensions.DependencyInjection;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Fluent API for building seeding pipelines with DI-based step registration and validation.
/// </summary>
/// <remarks>
/// Wraps <see cref="IServiceCollection"/> and a recipe name, tracking step count for
/// deterministic ordering and validation flags for dependency rules.
/// </remarks>
public class RecipeBuilder(string name, IServiceCollection services)
{
    private int _stepOrder;

    public string Name { get; } = name;

    public IServiceCollection Services { get; } = services;

    internal bool HasOrg { get; set; }

    internal bool HasOwner { get; set; }

    internal bool HasGenerator { get; set; }

    internal bool HasRosterUsers { get; set; }

    internal bool HasGeneratedUsers { get; set; }

    internal bool HasFixtureCiphers { get; set; }

    internal bool HasGeneratedCiphers { get; set; }

    internal bool HasFolders { get; set; }

    internal bool HasCipherFolderAssignment { get; set; }

    internal bool HasRosterOwner { get; set; }

    internal bool HasPersonalCiphers { get; set; }

    internal bool HasIndividualUser { get; set; }

    internal bool HasNamedFolders { get; set; }

    internal bool HasBilling { get; set; }

    /// <summary>
    /// Registers a step as a keyed singleton service with preserved ordering.
    /// </summary>
    /// <param name="factory">Factory function that creates the step from an IServiceProvider</param>
    /// <returns>This builder for fluent chaining</returns>
    public RecipeBuilder AddStep(Func<IServiceProvider, IStep> factory)
    {
        var order = _stepOrder++;
        Services.AddKeyedSingleton(Name, (sp, _) =>
        {
            var step = factory(sp);
            return new OrderedStep(step, order, step is IPostCommitStep);
        });
        return this;
    }

    /// <summary>
    /// Registers an asynchronous step as a keyed singleton service with preserved ordering.
    /// </summary>
    /// <param name="factory">Factory function that creates the step from an IServiceProvider</param>
    /// <returns>This builder for fluent chaining</returns>
    public RecipeBuilder AddAsyncStep(Func<IServiceProvider, IAsyncStep> factory)
    {
        var order = _stepOrder++;
        Services.AddKeyedSingleton(Name, (sp, _) =>
        {
            var step = factory(sp);
            return new OrderedStep(step, order, step is IPostCommitStep);
        });
        return this;
    }
}
