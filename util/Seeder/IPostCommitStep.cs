namespace Bit.Seeder;

/// <summary>
/// Marker applied alongside <see cref="IStep"/> or <see cref="IAsyncStep"/> to defer a step until
/// after <c>BulkCommitter.Commit</c> has flushed the pipeline's entities to the database.
/// </summary>
/// <remarks>
/// Post-commit steps observe committed rows but see cleared entity lists on the
/// <c>SeederContext</c>; the <c>EntityRegistry</c> and the context's scalar properties survive.
/// The execution result is snapshotted before the commit, so a post-commit step cannot contribute
/// to it.
/// </remarks>
public interface IPostCommitStep;
