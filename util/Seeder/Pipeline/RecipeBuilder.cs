using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Fluent API for building seeding pipelines with DI-based step registration and validation.
/// </summary>
/// <remarks>
/// Wraps <see cref="IServiceCollection"/> and a recipe name, tracking step count for
/// deterministic ordering and validation flags for dependency rules.
/// <strong>Phase Order:</strong> Org → OrgApiKey → Roster → Owner (if no roster owner) → Generator → Users → Groups → Collections → Folders → Ciphers → CipherCollections → CipherFolders → CipherFavorites → PersonalCiphers
/// Post-commit steps (e.g. Stripe finalization) run after the bulk commit, in registration order.
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
    /// <remarks>
    /// Steps execute in the order they are registered. Callers must register steps
    /// in the correct phase order: Org, OrgApiKey, Owner, Generator, Roster, Users, Groups,
    /// Collections, Folders, Ciphers, CipherCollections, CipherFolders, CipherFavorites, PersonalCiphers.
    /// </remarks>
    /// <param name="factory">Factory function that creates the step from an IServiceProvider</param>
    /// <returns>This builder for fluent chaining</returns>
    public RecipeBuilder AddStep(Func<IServiceProvider, IStep> factory)
    {
        var order = _stepOrder++;
        Services.AddKeyedSingleton<OrderedStep>(Name, (sp, _) =>
        {
            var step = factory(sp);
            return new OrderedStep(step, order, isPostCommit: step is IPostCommitStep);
        });
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
        Services.AddKeyedSingleton<OrderedStep>(Name, (sp, _) =>
        {
            var step = sp.GetRequiredService<T>();
            return new OrderedStep(step, order, isPostCommit: step is IPostCommitStep);
        });
        Services.TryAddSingleton<T>();
        return this;
    }

    /// <summary>
    /// Registers an asynchronous step as a keyed singleton with preserved ordering. Use for
    /// steps that perform I/O (e.g. calling external services like Stripe).
    /// </summary>
    /// <param name="factory">Factory function that creates the step from an IServiceProvider</param>
    /// <returns>This builder for fluent chaining</returns>
    public RecipeBuilder AddAsyncStep(Func<IServiceProvider, IAsyncStep> factory)
    {
        var order = _stepOrder++;
        Services.AddKeyedSingleton<OrderedStep>(Name, (sp, _) =>
        {
            var step = factory(sp);
            return new OrderedStep(step, order, isPostCommit: step is IPostCommitStep);
        });
        return this;
    }

    /// <summary>
    /// Registers an asynchronous step type as a keyed singleton with preserved ordering.
    /// </summary>
    /// <typeparam name="T">The step implementation type</typeparam>
    public RecipeBuilder AddAsyncStep<T>() where T : class, IAsyncStep
    {
        var order = _stepOrder++;
        Services.AddKeyedSingleton<OrderedStep>(Name, (sp, _) =>
        {
            var step = sp.GetRequiredService<T>();
            return new OrderedStep(step, order, isPostCommit: step is IPostCommitStep);
        });
        Services.TryAddSingleton<T>();
        return this;
    }
}
