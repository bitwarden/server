using System.Security.Claims;
using Bit.Api.Pam.Controllers;
using Bit.Api.Vault.Models.Response;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Models.Data;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;
using CipherType = Bit.Core.Vault.Enums.CipherType;

namespace Bit.Api.Test.Pam.Controllers;

[ControllerCustomize(typeof(CipherLeaseController))]
[SutProviderCustomize]
public class CipherLeaseControllerTests
{
    [Theory, BitAutoData]
    public async Task GetCipher_NoLeasedCipher_ThrowsNotFound(
        Guid id, User user, SutProvider<CipherLeaseController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);
        sutProvider.GetDependency<IGetLeasedCipherQuery>()
            .GetLeasedCipherAsync(user.Id, id)
            .ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetCipher(id));
    }

    [Theory, BitAutoData]
    public async Task GetCipher_LeasedCipher_ReturnsFullData(
        Guid id, Guid organizationId, User user, SutProvider<CipherLeaseController> sutProvider)
    {
        var cipher = new CipherDetails
        {
            Id = id,
            OrganizationId = organizationId,
            Type = CipherType.Login,
            Data = "2.iv|ct|mac",
        };
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);
        sutProvider.GetDependency<IGetLeasedCipherQuery>()
            .GetLeasedCipherAsync(user.Id, id)
            .Returns(cipher);
        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetManyByUserIdCipherIdAsync(user.Id, id)
            .Returns(new List<CollectionCipher>());
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { Id = organizationId });

        var result = await sutProvider.Sut.GetCipher(id);

        Assert.IsType<CipherDetailsResponseModel>(result);
        Assert.Equal(id, result.Id);
        Assert.Equal("2.iv|ct|mac", result.Data); // full data present
        Assert.Null(result.PartialData);           // isPartial == false
    }
}
