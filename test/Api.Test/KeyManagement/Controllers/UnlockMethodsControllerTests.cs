using System.Security.Claims;
using Bit.Api.KeyManagement.Controllers;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.KeyService;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Controllers;

[ControllerCustomize(typeof(UnlockMethodsController))]
[SutProviderCustomize]
public class UnlockMethodsControllerTests
{
    [Theory, BitAutoData]
    public async Task GetAll_PassesKinds(SutProvider<UnlockMethodsController> sutProvider, User user,
        UnlockMethodView method)
    {
        ArrangeUser(sutProvider, user);
        sutProvider.GetDependency<IUnlockMethodsQuery>()
            .Run(user, Arg.Is<IReadOnlyCollection<string>>(kinds =>
                kinds.SequenceEqual(new[] { UnlockMethodKinds.Device, UnlockMethodKinds.Passkey })))
            .Returns([method]);

        var result = await sutProvider.Sut.GetAllAsync("device,passkey");

        var resource = Assert.Single(result.Data);
        Assert.Equal(method.Id, resource.Id);
        Assert.Equal(KeyResourceTypes.UnlockMethod, resource.Type);
        Assert.Equal(method.Data, resource.Attributes.Data);
    }

    [Theory, BitAutoData]
    public async Task Get_ReturnsMethod(SutProvider<UnlockMethodsController> sutProvider, User user,
        UnlockMethodView method, UnlockMethodView other)
    {
        ArrangeUser(sutProvider, user);
        sutProvider.GetDependency<IUnlockMethodsQuery>().Run(user, null).Returns([other, method]);

        var result = await sutProvider.Sut.GetAsync(method.Id);

        Assert.Equal(method.Id, result.Data.Id);
    }

    [Theory, BitAutoData]
    public async Task Get_Unknown_NotFound(SutProvider<UnlockMethodsController> sutProvider, User user,
        UnlockMethodView method)
    {
        ArrangeUser(sutProvider, user);
        sutProvider.GetDependency<IUnlockMethodsQuery>().Run(user, null).Returns([method]);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAsync(Guid.NewGuid()));
    }

    private static void ArrangeUser(SutProvider<UnlockMethodsController> sutProvider, User user) =>
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
}
