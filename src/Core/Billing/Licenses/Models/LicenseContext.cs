#nullable enable
using Bit.Core.Models.Business;

namespace Bit.Core.Billing.Licenses.Models;

public class LicenseContext
{
    public Guid? InstallationId { get; init; }
    public required SubscriptionInfo SubscriptionInfo { get; init; }
}
