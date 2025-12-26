using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Entities;

/// <summary>
/// PlayData is a join table tracking entities created during automated testing.
/// A `PlayId` is supplied by the clients in the `x-play-id` header to inform the server
/// that any data created should be associated with the play, and therefore cleaned up with it.
/// </summary>
public class PlayData : ITableObject<Guid>
{
    public Guid Id { get; set; }
    [MaxLength(256)]
    public required string PlayId { get; init; }
    public Guid? UserId { get; init; }
    public Guid? OrganizationId { get; init; }
    public DateTime CreationDate { get; init; }

    /// <summary>
    /// Generates and sets a new COMB GUID for the Id property.
    /// </summary>
    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }

    /// <summary>
    /// Creates a new PlayData record associated with a User.
    /// </summary>
    /// <param name="user">The user entity created during the play.</param>
    /// <param name="playId">The play identifier from the x-play-id header.</param>
    /// <returns>A new PlayData instance tracking the user.</returns>
    public static PlayData Create(User user, string playId)
    {
        return new PlayData
        {
            PlayId = playId,
            UserId = user.Id,
            CreationDate = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new PlayData record associated with an Organization.
    /// </summary>
    /// <param name="organization">The organization entity created during the play.</param>
    /// <param name="playId">The play identifier from the x-play-id header.</param>
    /// <returns>A new PlayData instance tracking the organization.</returns>
    public static PlayData Create(Organization organization, string playId)
    {
        return new PlayData
        {
            PlayId = playId,
            OrganizationId = organization.Id,
            CreationDate = DateTime.UtcNow
        };
    }
}
