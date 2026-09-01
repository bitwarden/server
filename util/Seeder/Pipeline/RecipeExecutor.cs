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
    /// Executes the recipe by resolving keyed steps, running the pre-commit steps in order, committing,
    /// then running any steps marked <see cref="IPostCommitStep"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Clears the EntityRegistry at the start to ensure a clean slate for each run.
    /// </para>
    /// <para>
    /// <strong>Steps are awaited strictly sequentially.</strong> Never batch them with
    /// <c>Task.WhenAll</c>: <see cref="SeederContext"/> is not thread-safe and each step reads state
    /// written by the ones before it. Synchronous steps return an already-completed task, so they
    /// still run inline on the calling thread in registration order.
    /// </para>
    /// <para>
    /// The returned <see cref="PipelineExecutionResult"/> is snapshotted <em>before</em> the commit,
    /// because committing clears the context's entity lists. A post-commit step therefore cannot
    /// contribute to it. A caller that needs post-commit values in the result should append
    /// <c>return result with { ... };</c> after the post-commit loop rather than moving the snapshot —
    /// several of its arguments read <c>.Count</c> off lists the committer has already cleared.
    /// </para>
    /// </remarks>
    internal async Task<PipelineExecutionResult> ExecuteAsync()
    {
        var steps = _serviceProvider.GetKeyedServices<OrderedStep>(_recipeName)
            .OrderBy(s => s.Order)
            .ToList();

        var context = new SeederContext(_serviceProvider);
        context.Registry.Clear();

        foreach (var step in steps.Where(s => !s.IsPostCommit))
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
            context.Folders.Count,
            context.SsoIdentifier);

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

        var postCommit = steps.Where(s => s.IsPostCommit).ToList();
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

        // The sanctioned post-commit extension documented above: gateway IDs are written onto the
        // organization by FinalizeOrganizationBillingStep, long after the snapshot was taken.
        return result with
        {
            GatewayCustomerId = context.Organization?.GatewayCustomerId,
            GatewaySubscriptionId = context.Organization?.GatewaySubscriptionId,
        };
    }
}
