using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using OneOf;

namespace Bit.Core.Billing.Providers.Services;

public interface IBusinessUnitConverter
{
    /// <summary>
    /// Finalizes the process of converting the <paramref name="organization"/> to a <see cref="ProviderType.BusinessUnit"/> by
    /// saving all the necessary key provided by the client and updating the <paramref name="organization"/>'s subscription to a
    /// provider subscription.
    /// </summary>
    /// <param name="organization">The organization to convert to a business unit.</param>
    /// <param name="userId">The ID of the organization member who will be the provider admin.</param>
    /// <param name="token">The token sent to the client as part of the <see cref="InitiateConversion"/> process.</param>
    /// <param name="providerKey">The encrypted provider key used to enable the <see cref="ProviderUser"/>.</param>
    /// <param name="organizationKey">The encrypted organization key used to enable the <see cref="ProviderOrganization"/>.</param>
    /// <returns>The provider ID</returns>
    Task<Guid> FinalizeConversion(
        Organization organization,
        Guid userId,
        string token,
        string providerKey,
        string organizationKey);

    /// <summary>
    /// Begins the process of converting the <paramref name="organization"/> to a <see cref="ProviderType.BusinessUnit"/> by
    /// creating all the necessary database entities and sending a setup invitation to the <paramref name="providerAdminEmail"/>.
    /// </summary>
    /// <param name="organization">The organization to convert to a business unit.</param>
    /// <param name="providerAdminEmail">The email address of the organization member who will be the provider admin.</param>
    /// <returns>Either the newly created provider ID or a list of validation failures.</returns>
    Task<OneOf<Guid, List<string>>> InitiateConversion(
        Organization organization,
        string providerAdminEmail);

    /// <summary>
    /// Checks if the <paramref name="organization"/> has a business unit conversion in progress and, if it does, resends the
    /// setup invitation to the provider admin.
    /// </summary>
    /// <param name="organization">The organization to convert to a business unit.</param>
    /// <param name="providerAdminEmail">The email address of the organization member who will be the provider admin.</param>
    Task ResendConversionInvite(
        Organization organization,
        string providerAdminEmail);

    /// <summary>
    /// Checks if the <paramref name="organization"/> has a business unit conversion in progress and, if it does, resets that conversion
    /// by deleting all the database entities created as part of <see cref="InitiateConversion"/>.
    /// </summary>
    /// <param name="organization">The organization to convert to a business unit.</param>
    /// <param name="providerAdminEmail">The email address of the organization member who will be the provider admin.</param>
    Task ResetConversion(
        Organization organization,
        string providerAdminEmail);
}
