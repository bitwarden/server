namespace Bit.Seeder.Pipeline;

internal sealed class RecipeExecutor(BulkCommitter committer)
{
    /// <summary>
    /// Executes a recipe pipeline by running all steps in sequence and persisting the results.
    /// </summary>
    /// <param name="steps">The ordered sequence of steps to execute.</param>
    /// <param name="context">The seeder context containing shared state.</param>
    /// <returns>Execution result with organization ID and entity statistics.</returns>
    /// <remarks>
    /// IMPORTANT: This method clears the EntityRegistry at the start to ensure a clean slate.
    /// Any entities registered in the context from previous executions will be removed.
    ///
    /// Entity counts are captured BEFORE committing to the database, since BulkCommitter
    /// clears the entity lists after bulk copy for memory efficiency.
    /// </remarks>
    internal ExecutionResult Execute(IReadOnlyList<IStep> steps, SeederContext context)
    {
        context.Registry.Clear();

        foreach (var step in steps)
        {
            step.Execute(context);
        }

        // Capture counts BEFORE committing (commit clears the lists)
        var result = new ExecutionResult(
            context.RequireOrgId(),
            context.Owner?.Email,
            context.Users.Count,
            context.Groups.Count,
            context.Collections.Count,
            context.Ciphers.Count);

        committer.Commit(context);
        return result;
    }
}
