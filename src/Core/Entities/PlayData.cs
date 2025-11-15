using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Entities;

public class PlayData : ITableObject<Guid>
{
    public Guid Id { get; set; }
    [MaxLength(256)]
    public string PlayId { get; init; } = null!;
    public Guid? UserId { get; init; }
    public Guid? OrganizationId { get; init; }
    public DateTime CreationDate { get; init; }

    protected PlayData() { }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }

    public static PlayData Create(User user, string playId)
    {
        return new PlayData
        {
            PlayId = playId,
            UserId = user.Id,
            CreationDate = DateTime.UtcNow
        };
    }

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
