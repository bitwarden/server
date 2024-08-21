#nullable enable
using Bit.Core.NotificationCenter.Enums;

namespace Bit.Core.NotificationCenter.Models.Filter;

public class NotificationFilter
{
    public ClientType ClientType { get; set; } = ClientType.All;
    public IEnumerable<Guid>? OrganizationIds { get; set; }
}
