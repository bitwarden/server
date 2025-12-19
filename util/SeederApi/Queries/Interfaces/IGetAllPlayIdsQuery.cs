namespace Bit.SeederApi.Queries.Interfaces;

/// <summary>
/// Query for retrieving all play IDs for currently tracked seeded data.
/// </summary>
public interface IGetAllPlayIdsQuery
{
    /// <summary>
    /// Retrieves all play IDs for currently tracked seeded data.
    /// </summary>
    /// <returns>A list of play IDs representing active seeded data that can be destroyed.</returns>
    List<string> GetAllPlayIds();
}
