using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Core.Vault.Queries;

public class GetOrganizationCipherTypeCountsQuery(
    ICipherRepository cipherRepository,
    IFeatureService featureService)
    : IGetOrganizationCipherTypeCountsQuery
{
    /// <summary>
    /// Cipher types that only exist while <see cref="FeatureFlagKeys.PM32009_NewItemTypes"/> is enabled. Keep in
    /// sync with the types withheld from sync responses in <c>SyncController</c>.
    /// </summary>
    private static readonly CipherType[] _newItemTypes =
    [
        CipherType.BankAccount,
        CipherType.DriversLicense,
        CipherType.Passport,
    ];

    public async Task<IReadOnlyDictionary<CipherType, int>> GetCountsAsync(Guid organizationId)
    {
        var counts = await cipherRepository.GetCountsByTypeByOrganizationIdAsync(organizationId);
        var newItemTypesEnabled = featureService.IsEnabled(FeatureFlagKeys.PM32009_NewItemTypes);

        return Enum.GetValues<CipherType>()
            .Where(type => newItemTypesEnabled || !_newItemTypes.Contains(type))
            .ToDictionary(type => type, type => counts.GetValueOrDefault(type));
    }
}
