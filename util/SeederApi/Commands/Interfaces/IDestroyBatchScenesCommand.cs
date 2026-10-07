namespace Bit.SeederApi.Commands.Interfaces;

/// <summary>
/// Command for destroying multiple scenes in parallel.
/// </summary>
public interface IDestroyBatchScenesCommand
{
    /// <summary>
    /// Destroys multiple scenes by their play IDs in parallel.
    /// </summary>
    /// <param name="playIds">The list of play IDs to destroy</param>
    /// <exception cref="AggregateException">Thrown when one or more scenes fail to destroy</exception>
    Task DestroyAsync(IEnumerable<string> playIds);
}
