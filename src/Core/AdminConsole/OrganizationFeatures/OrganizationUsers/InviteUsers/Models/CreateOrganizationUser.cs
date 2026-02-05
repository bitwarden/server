// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;

/// <summary>
/// Object for associating the <see cref="OrganizationUser"/> with their assigned collections
/// <see cref="CollectionAccessSelection"/> and Group Ids.
/// </summary>
public class CreateOrganizationUser
{
    public OrganizationUser OrganizationUser { get; set; }
    public CollectionAccessSelection[] Collections { get; set; } = [];
    public Guid[] Groups { get; set; } = [];
}
