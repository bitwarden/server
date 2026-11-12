using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Queries;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Queries;

[SutProviderCustomize]
public class GetOrganizationCipherTypeCountsQueryTests
{
    private static readonly CipherType[] _newItemTypes =
        [CipherType.BankAccount, CipherType.DriversLicense, CipherType.Passport];

    [Theory, BitAutoData]
    public async Task GetCountsAsync_NewItemTypesEnabled_ReturnsRepositoryCounts(
        Guid organizationId,
        SutProvider<GetOrganizationCipherTypeCountsQuery> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PM32009_NewItemTypes).Returns(true);
        sutProvider.GetDependency<ICipherRepository>()
            .GetCountsByTypeByOrganizationIdAsync(organizationId)
            .Returns(new Dictionary<CipherType, int>
            {
                [CipherType.Card] = 8,
                [CipherType.BankAccount] = 3,
            });

        var result = await sutProvider.Sut.GetCountsAsync(organizationId);

        Assert.Equal(8, result[CipherType.Card]);
        Assert.Equal(3, result[CipherType.BankAccount]);
    }

    [Theory, BitAutoData]
    public async Task GetCountsAsync_NewItemTypesEnabled_ZeroFillsEveryType(
        Guid organizationId,
        SutProvider<GetOrganizationCipherTypeCountsQuery> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PM32009_NewItemTypes).Returns(true);
        sutProvider.GetDependency<ICipherRepository>()
            .GetCountsByTypeByOrganizationIdAsync(organizationId)
            .Returns(new Dictionary<CipherType, int> { [CipherType.Card] = 8 });

        var result = await sutProvider.Sut.GetCountsAsync(organizationId);

        Assert.Equal(Enum.GetValues<CipherType>().Length, result.Count);
        Assert.All(
            Enum.GetValues<CipherType>().Where(t => t != CipherType.Card),
            type => Assert.Equal(0, result[type]));
    }

    [Theory, BitAutoData]
    public async Task GetCountsAsync_NewItemTypesDisabled_OmitsNewItemTypes(
        Guid organizationId,
        SutProvider<GetOrganizationCipherTypeCountsQuery> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PM32009_NewItemTypes).Returns(false);
        // Items of the new types may still exist, e.g. if the flag was turned off after they were created.
        sutProvider.GetDependency<ICipherRepository>()
            .GetCountsByTypeByOrganizationIdAsync(organizationId)
            .Returns(new Dictionary<CipherType, int>
            {
                [CipherType.Card] = 8,
                [CipherType.BankAccount] = 3,
            });

        var result = await sutProvider.Sut.GetCountsAsync(organizationId);

        Assert.All(_newItemTypes, type => Assert.DoesNotContain(type, result.Keys));
        Assert.Equal(8, result[CipherType.Card]);
        Assert.Equal(0, result[CipherType.Login]);
        Assert.Equal(Enum.GetValues<CipherType>().Length - _newItemTypes.Length, result.Count);
    }

    [Theory, BitAutoData]
    public async Task GetCountsAsync_EmptyOrganization_ReturnsAllAvailableTypesAsZero(
        Guid organizationId,
        SutProvider<GetOrganizationCipherTypeCountsQuery> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PM32009_NewItemTypes).Returns(true);
        sutProvider.GetDependency<ICipherRepository>()
            .GetCountsByTypeByOrganizationIdAsync(organizationId)
            .Returns(new Dictionary<CipherType, int>());

        var result = await sutProvider.Sut.GetCountsAsync(organizationId);

        Assert.Equal(Enum.GetValues<CipherType>().Length, result.Count);
        Assert.All(result.Values, count => Assert.Equal(0, count));
    }
}
