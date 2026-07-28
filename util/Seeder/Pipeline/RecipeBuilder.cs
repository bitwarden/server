using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Fluent API for building seeding pipelines with DI-based step registration and validation.
/// </summary>
/// <remarks>
/// Wraps <see cref="IServiceCollection"/> and a recipe name, tracking step count for
/// deterministic ordering and validation flags for dependency rules.
/// </remarks>
public class RecipeBuilder
{
    private int _stepOrder;

    public RecipeBuilder(string name, IServiceCollection services)
    {
        Name = name;
        Services = services;
    }

    public string Name { get; }

    public IServiceCollection Services { get; }

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

    /// <summary>
    /// Registers a step as a keyed singleton service with preserved ordering.
    /// </summary>
    /// <param name="factory">Factory function that creates the step from an IServiceProvider</param>
    /// <returns>This builder for fluent chaining</returns>
    public RecipeBuilder AddStep(Func<IServiceProvider, IStep> factory)
    {
        var order = _stepOrder++;
        Services.AddKeyedSingleton<IStep>(Name, (sp, _) => new OrderedStep(factory(sp), order));
        return this;
    }

    /// <summary>
    /// Registers a step type as a keyed singleton with preserved ordering.
    /// </summary>
    /// <typeparam name="T">The step implementation type</typeparam>
    /// <returns>This builder for fluent chaining</returns>
    public RecipeBuilder AddStep<T>() where T : class, IStep
    {
        var order = _stepOrder++;
        Services.AddKeyedSingleton<IStep>(Name, (sp, _) => new OrderedStep(sp.GetRequiredService<T>(), order));
        Services.TryAddSingleton<T>();
        return this;
    }
}
