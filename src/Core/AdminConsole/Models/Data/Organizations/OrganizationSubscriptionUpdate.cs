using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.AdminConsole.Models.Data.Organizations;

public class OrganizationSubscriptionUpdate
{
    public required Organization Organization { get; set; }
    public required int Seats { get; set; }
    public Plan? Plan { get; set; }
}
