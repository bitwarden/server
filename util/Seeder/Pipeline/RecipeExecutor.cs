using Bit.Seeder.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Seeder.Pipeline;

/// <summary>
/// Resolves steps from DI by recipe key and executes them in order.
/// </summary>
internal sealed class RecipeExecutor
{
    private readonly string _recipeName;

    private readonly IServiceProvider _serviceProvider;

    private readonly BulkCommitter _committer;

    internal RecipeExecutor(string recipeName, IServiceProvider serviceProvider, BulkCommitter committer)
    {
        _recipeName = recipeName;
        _serviceProvider = serviceProvider;
        _committer = committer;
    }

    /// <summary>
    /// Executes the recipe by resolving keyed steps, running them in order, committing
    /// accumulated entities, then running any post-commit steps.
    /// </summary>
    /// <remarks>
    /// Steps are registered as <see cref="OrderedStep"/> keyed by recipe name and resolved
    /// here. Each <see cref="OrderedStep"/> tracks its order index and whether it runs
    /// before or after <see cref="BulkCommitter.Commit"/>.
    /// Clears the EntityRegistry at the start to ensure a clean slate for each run.
    /// </remarks>
    internal async Task<PipelineExecutionResult> ExecuteAsync()
    {
        var steps = _serviceProvider.GetKeyedServices<OrderedStep>(_recipeName)
            .OrderBy(s => s.Order)
            .ToList();

        var preCommit = steps.Where(s => !s.IsPostCommit).ToList();
        var postCommit = steps.Where(s => s.IsPostCommit).ToList();

        var context = new SeederContext(_serviceProvider);
        context.Registry.Clear();

        foreach (var step in preCommit)
        {
            await step.ExecuteAsync(context);
        }

        // Capture counts BEFORE committing (commit clears the lists)
        var result = new PipelineExecutionResult(
            context.Organization?.Id,
            context.Owner?.Id,
            context.Owner?.Email,
            context.Owner?.ApiKey,
            context.OrganizationApiKey?.ApiKey,
            context.GetPassword(),
            context.Owner?.Premium ?? false,
            context.Users.Count,
            context.Groups.Count,
            context.Collections.Count,
            context.Ciphers.Count,
            context.Folders.Count);

        var progress = context.GetProgress();
        progress?.Report(new PhaseStarted(SeederPhases.CommittingToDatabase, null));
        try
        {
            _committer.Commit(context);
        }
        finally
        {
            progress?.Report(new PhaseCompleted(SeederPhases.CommittingToDatabase));
        }

        if (postCommit.Count > 0)
        {
            progress?.Report(new PhaseStarted(SeederPhases.PostCommit, null));
            try
            {
                foreach (var step in postCommit)
                {
                    await step.ExecuteAsync(context);
                }
            }
            finally
            {
                progress?.Report(new PhaseCompleted(SeederPhases.PostCommit));
            }
        }

        return result;
    }
}
