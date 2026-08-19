namespace Bit.Seeder;

/// <summary>
/// Marker applied alongside <see cref="IStep"/> or <see cref="IAsyncStep"/> to defer a step until
/// after <c>BulkCommitter.Commit</c> has flushed the pipeline's entities to the database.
/// </summary>
/// <remarks>
/// Post-commit steps observe committed rows but see cleared entity lists on the
/// <c>SeederContext</c>; the <c>EntityRegistry</c> and the context's scalar properties survive.
/// The execution result is snapshotted before the commit, so a post-commit step generally cannot
/// contribute to it. The one sanctioned exception: <see cref="Pipeline.RecipeExecutor"/> re-projects
/// <c>context.Organization</c>'s gateway IDs onto the result after the post-commit loop runs, so a
/// step that writes them onto <c>SeederContext.Organization</c> (<c>FinalizeOrganizationBillingStep</c>
/// today) is the one way a post-commit step's work reaches the caller.
/// </remarks>
public interface IPostCommitStep;
