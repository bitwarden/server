using Bit.Core.AdminConsole.Entities;
using OneOf;

namespace Bit.Core.Billing.Providers.Services.NoopImplementations;

/// <summary>
/// A no-op implementation of <see cref="IBusinessUnitConverter"/> for use in OSS (non-commercial) builds.
/// Business unit conversion is a commercial feature and is not available in OSS deployments.
/// All methods throw <see cref="NotSupportedException"/> to indicate the feature is unavailable.
/// </summary>
public class NoopBusinessUnitConverter : IBusinessUnitConverter
{
    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// Always thrown because business unit conversion is not available in OSS builds.
    /// </exception>
    public Task<Guid> FinalizeConversion(
        Organization organization,
        Guid userId,
        string token,
        string providerKey,
        string organizationKey)
    {
        throw new NotSupportedException(
            "Business unit conversion is not available in non-commercial Bitwarden builds.");
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// Always thrown because business unit conversion is not available in OSS builds.
    /// </exception>
    public Task<OneOf<Guid, List<string>>> InitiateConversion(
        Organization organization,
        string providerAdminEmail)
    {
        throw new NotSupportedException(
            "Business unit conversion is not available in non-commercial Bitwarden builds.");
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// Always thrown because business unit conversion is not available in OSS builds.
    /// </exception>
    public Task ResendConversionInvite(
        Organization organization,
        string providerAdminEmail)
    {
        throw new NotSupportedException(
            "Business unit conversion is not available in non-commercial Bitwarden builds.");
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// Always thrown because business unit conversion is not available in OSS builds.
    /// </exception>
    public Task ResetConversion(
        Organization organization,
        string providerAdminEmail)
    {
        throw new NotSupportedException(
            "Business unit conversion is not available in non-commercial Bitwarden builds.");
    }
}
