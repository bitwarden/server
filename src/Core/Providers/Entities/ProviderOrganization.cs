using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Entities.Provider;

/// <summary>
/// A join record linking a <see cref="Provider"/> to a client <see cref="Bit.Core.AdminConsole.Entities.Organization"/>
/// that it manages.
/// </summary>
public class ProviderOrganization : ITableObject<Guid>
{
    /// <summary>
    /// A unique identifier for the provider-organization relationship.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// The ID of the <see cref="Provider"/> that manages the organization.
    /// </summary>
    public Guid ProviderId { get; set; }
    /// <summary>
    /// The ID of the <see cref="Bit.Core.AdminConsole.Entities.Organization"/> being managed.
    /// </summary>
    public Guid OrganizationId { get; set; }
    /// <summary>
    /// The organization's symmetric key, encrypted with the provider's symmetric key.
    /// </summary>
    public string? Key { get; set; }
    /// <summary>
    /// A JSON blob of provider-specific configuration for this organization.
    /// </summary>
    public string? Settings { get; set; }
    /// <summary>
    /// The date the provider-organization relationship was created.
    /// </summary>
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    /// <summary>
    /// The date the provider-organization relationship was last updated.
    /// </summary>
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    /// <summary>
    /// Initializes <see cref="Id"/> to a new COMB GUID.
    /// </summary>
    public void SetNewId()
    {
        if (Id == default)
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
