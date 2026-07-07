using System.Security.Claims;
using Bit.Api.Pam.Controllers;
using Bit.Api.Vault.Models.Response;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Models.Data;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;
using CipherType = Bit.Core.Vault.Enums.CipherType;

namespace Bit.Services.Pam.Test.Api.Controllers;

// The deprecated GET /leases/ciphers/{id}/cipher endpoint stays in the Api project (it depends on the Vault
// response models), so it is tested here as an MVC controller. The rest of the cipher-lease resource lives in
// the Commercial.Pam Minimal API handler (see CipherLeaseEndpointsHandlerTests).
[ControllerCustomize(typeof(CipherLeaseController))]
[SutProviderCustomize]
[Bit.Api.Test.Vault.AutoFixture.CipherLeaseGateBypassCustomize]
public class CipherLeaseControllerTests
{
    // GET /leases/ciphers/{id}/cipher is [Obsolete] (deprecated, scheduled for removal) but still fully functional, so
    // its behaviour stays under test; suppress the obsolete-usage warning for these deliberate calls.
#pragma warning disable CS0618
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
        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility { Id = organizationId });

        var result = await sutProvider.Sut.GetCipher(id);

        Assert.IsAssignableFrom<CipherDetailsResponseModel>(result);
        Assert.Equal(id, result.Id);
        Assert.Equal("2.iv|ct|mac", result.Data); // full data present
        Assert.Null(result.PartialData);           // isPartial == false
    }
#pragma warning restore CS0618
}
