using System.Security.Claims;
using Bit.Api.KeyManagement.Controllers;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.KeyService;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Controllers;

[ControllerCustomize(typeof(KeyServiceKeysController))]
[SutProviderCustomize]
public class KeyServiceKeysControllerTests
{
    [Theory, BitAutoData]
    public async Task PublicKeys_NoOwner_Refused(SutProvider<KeyServiceKeysController> sutProvider)
    {
        var result = await sutProvider.Sut.GetPublicKeysAsync(null, null, null, null, null);

        Assert.IsType<UnprocessableEntityObjectResult>(result.Result);
        await sutProvider.GetDependency<IPublicKeysQuery>().DidNotReceiveWithAnyArgs()
            .Run(default, default!, default!);
    }

    [Theory, BitAutoData]
    public async Task PublicKeys_TwoOwnerTypes_Refused(SutProvider<KeyServiceKeysController> sutProvider,
        Guid userId, Guid organizationId)
    {
        var result = await sutProvider.Sut.GetPublicKeysAsync(userId.ToString(), organizationId.ToString(), null,
            null, null);

        Assert.IsType<UnprocessableEntityObjectResult>(result.Result);
    }

    [Theory]
    [BitAutoData("not-an-id")]
    [BitAutoData(",")]
    public async Task PublicKeys_BadOwner_Refused(string user, SutProvider<KeyServiceKeysController> sutProvider)
    {
        var result = await sutProvider.Sut.GetPublicKeysAsync(user, null, null, null, null);

        Assert.IsType<UnprocessableEntityObjectResult>(result.Result);
    }

    [Theory, BitAutoData]
    public async Task PublicKeys_TooManyOwners_Refused(SutProvider<KeyServiceKeysController> sutProvider)
    {
        var users = string.Join(',', Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()));

        var result = await sutProvider.Sut.GetPublicKeysAsync(users, null, null, null, null);

        Assert.IsType<UnprocessableEntityObjectResult>(result.Result);
    }

    [Theory, BitAutoData]
    public async Task PublicKeys_ReturnsKeys(SutProvider<KeyServiceKeysController> sutProvider,
        Guid firstUser, Guid secondUser, PublicKeyView key)
    {
        sutProvider.GetDependency<IPublicKeysQuery>()
            .Run(KeyOwnerType.User,
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { firstUser, secondUser })),
                Arg.Is<KeyFilter>(filter => filter.KeyPurposes!.Single() == KeyPurposes.UserSharingRoot &&
                                            filter.KeyIds == null))
            .Returns([key with { Owner = new KeyOwner(KeyOwnerType.User, firstUser) }]);

        var result = await sutProvider.Sut.GetPublicKeysAsync($"{firstUser}, {secondUser}", null, null,
            KeyPurposes.UserSharingRoot, null);

        var resource = Assert.Single(result.Value!.Data);
        Assert.Equal(key.Id, resource.Id);
        Assert.Equal(KeyResourceTypes.PublicKey, resource.Type);
        Assert.Equal("user", resource.Attributes.Owner.Type);
        Assert.Equal(firstUser, resource.Attributes.Owner.Id);
        Assert.Equal(key.Data, resource.Attributes.Data);
    }

    [Theory, BitAutoData]
    public async Task OwnKeys_ReadsCurrentUser(SutProvider<KeyServiceKeysController> sutProvider, User user,
        OwnKeyView key)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        sutProvider.GetDependency<IOwnKeysQuery>()
            .Run(user, Arg.Is<KeyFilter>(filter => filter.KeyIds!.Single() == key.KeyId))
            .Returns([key with { Owner = new KeyOwner(KeyOwnerType.User, user.Id) }]);

        var result = await sutProvider.Sut.GetOwnKeysAsync(null, key.KeyId);

        var resource = Assert.Single(result.Data);
        Assert.Equal(key.Type, resource.Type);
        Assert.Equal(key.SealedByKeyId, resource.Attributes.SealedByKeyId);
    }
}
