namespace Bit.Seeder;

/// <summary>
/// Marker interface for steps that must run after <see cref="Pipeline.BulkCommitter"/> has
/// flushed accumulated entities to the database — e.g. steps that update an existing row
/// via a repository and therefore require the row to already exist.
/// </summary>
/// <remarks>
/// Apply alongside either <see cref="IStep"/> or <see cref="IAsyncStep"/>.
/// </remarks>
public interface IPostCommitStep
{
}
