using Bit.Core.Utilities;

namespace Bit.Core.Entities.Provider;

public class ProviderOrganization : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Guid OrganizationId { get; set; }
    public string Key { get; set; }
    public string Settings { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
