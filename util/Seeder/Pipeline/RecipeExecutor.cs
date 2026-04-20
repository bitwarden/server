using Bit.Seeder.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Seeder.Pipeline;

internal static class SeederPhases
{
    internal const string CommittingToDatabase = "Committing to database";
}

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
    /// Executes the recipe by resolving keyed steps, running them in order, and committing results.
    /// </summary>
    /// <remarks>
    /// Clears the EntityRegistry at the start to ensure a clean slate for each run.
    /// </remarks>
    internal PipelineExecutionResult Execute()
    {
        var steps = _serviceProvider.GetKeyedServices<IStep>(_recipeName)
            .OrderBy(s => s is OrderedStep os ? os.Order : int.MaxValue)
            .ToList();

        var context = new SeederContext(_serviceProvider);
        context.Registry.Clear();

        foreach (var step in steps)
        {
            step.Execute(context);
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

        return result;
    }
}
