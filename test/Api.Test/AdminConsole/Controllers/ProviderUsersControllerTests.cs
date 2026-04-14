using Bit.Api.AdminConsole.Controllers;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(ProviderUsersController))]
[SutProviderCustomize]
public class ProviderUsersControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task Get_ProviderUserNotFound_ThrowsNotFound(Guid providerId, Guid id,
        SutProvider<ProviderUsersController> sutProvider)
    {
        sutProvider.GetDependency<IProviderUserRepository>().GetByIdAsync(id).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Get(providerId, id));
    }

    [Theory]
    [BitAutoData]
    public async Task Get_ProviderIdMismatch_ThrowsNotFound(Guid providerId, ProviderUser providerUser,
        SutProvider<ProviderUsersController> sutProvider)
    {
        sutProvider.GetDependency<IProviderUserRepository>().GetByIdAsync(providerUser.Id).Returns(providerUser);
        sutProvider.GetDependency<ICurrentContext>().ProviderManageUsers(providerUser.ProviderId).Returns(true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Get(providerId, providerUser.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task Get_NoPermission_ThrowsNotFound(ProviderUser providerUser,
        SutProvider<ProviderUsersController> sutProvider)
    {
        sutProvider.GetDependency<IProviderUserRepository>().GetByIdAsync(providerUser.Id).Returns(providerUser);
        sutProvider.GetDependency<ICurrentContext>().ProviderManageUsers(providerUser.ProviderId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.Get(providerUser.ProviderId, providerUser.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task Get_Success(ProviderUser providerUser,
        SutProvider<ProviderUsersController> sutProvider)
    {
        // Permissions must be valid JSON for ProviderUserResponseModel constructor
        providerUser.Permissions = null;
        sutProvider.GetDependency<IProviderUserRepository>().GetByIdAsync(providerUser.Id).Returns(providerUser);
        sutProvider.GetDependency<ICurrentContext>().ProviderManageUsers(providerUser.ProviderId).Returns(true);

        var result = await sutProvider.Sut.Get(providerUser.ProviderId, providerUser.Id);

        Assert.Equal(providerUser.Id, result.Id);
    }
}
