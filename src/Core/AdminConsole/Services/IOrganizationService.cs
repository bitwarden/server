// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;

namespace Bit.Core.Services;

public interface IOrganizationService
{
    Task ReinstateSubscriptionAsync(Guid organizationId);
    Task<string> AdjustStorageAsync(Guid organizationId, short storageAdjustmentGb);
    Task UpdateSubscription(Guid organizationId, int seatAdjustment, int? maxAutoscaleSeats);
    Task AutoAddSeatsAsync(Organization organization, int seatsToAdd);
    Task<string> AdjustSeatsAsync(Guid organizationId, int seatAdjustment);
    Task UpdateExpirationDateAsync(Guid organizationId, DateTime? expirationDate);
    Task UpdateAsync(Organization organization, bool updateBilling = false);
    Task UpdateTwoFactorProviderAsync(Organization organization, TwoFactorProviderType type);
    /// <summary>
    /// Removes the entry for <paramref name="type"/> from the organization's <c>TwoFactorProviders</c> JSON column.
    /// The provider's <c>MetaData</c> (Duo Host / ClientId / ClientSecret) is destroyed in the process; the name is
    /// historical — this is a hard delete of the provider configuration, not a reversible disable. No-op if
    /// <paramref name="type"/> is not currently configured. Throws <see cref="ArgumentException"/> if
    /// <paramref name="type"/> is not an organization-scoped provider type.
    /// </summary>
    Task DisableTwoFactorProviderAsync(Organization organization, TwoFactorProviderType type);
    Task<OrganizationUser> InviteUserAsync(Guid organizationId, Guid? invitingUserId, EventSystemUser? systemUser,
        OrganizationUserInvite invite, string externalId);
    Task<List<OrganizationUser>> InviteUsersAsync(Guid organizationId, Guid? invitingUserId, EventSystemUser? systemUser,
        IEnumerable<(OrganizationUserInvite invite, string externalId)> invites);
    Task DeleteSsoUserAsync(Guid userId, Guid? organizationId);
    Task ReplaceAndUpdateCacheAsync(Organization org, EventType? orgEvent = null);
    Task<(bool canScale, string failureReason)> CanScaleAsync(Organization organization, int seatsToAdd);

    void ValidatePasswordManagerPlan(Models.StaticStore.Plan plan, OrganizationUpgrade upgrade);
    void ValidateSecretsManagerPlan(Models.StaticStore.Plan plan, OrganizationUpgrade upgrade);
    Task ValidateOrganizationUserUpdatePermissions(Guid organizationId, OrganizationUserType newType,
        OrganizationUserType? oldType, Permissions permissions);
    Task ValidateOrganizationCustomPermissionsEnabledAsync(Guid organizationId, OrganizationUserType newType);
}
