using Bit.Core.AdminConsole.Entities;

namespace Bit.Api.AdminConsole.Partnerships;

/// <summary>
/// The partnership of the organization that authenticated the current partner API request.
/// </summary>
public interface IPartnershipRequestContext
{
    /// <summary>
    /// The active partnership resolved by <see cref="PartnershipAccessFilter"/>. Only available on partner controllers.
    /// </summary>
    /// <exception cref="InvalidOperationException">The partnership has not been resolved for this request.</exception>
    OrganizationPartnership Partnership { get; }
}
