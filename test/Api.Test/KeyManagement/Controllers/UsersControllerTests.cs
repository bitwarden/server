#nullable enable
using Bit.Api.KeyManagement.Controllers;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Controllers;

[ControllerCustomize(typeof(UsersController))]
[SutProviderCustomize]
[JsonDocumentCustomize]
public class UsersControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task GetPublicKey_NotFound_ThrowsNotFoundException(
        SutProvider<UsersController> sutProvider)
    {
        sutProvider.GetDependency<IUserRepository>().GetPublicKeyAsync(Arg.Any<Guid>()).ReturnsNull();
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetPublicKey("id"));
    }

    [Theory]
    [BitAutoData]
    public async Task GetAccountKeys_UserNotFound_ThrowsNotFoundException(
        SutProvider<UsersController> sutProvider)
    {
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(Arg.Any<Guid>()).ReturnsNull();
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAccountKeys("id"));
    }

    [Theory]
    [BitAutoData]
    public async Task GetAccountKeys_ReturnsPublicUserKeysResponseModel(
        SutProvider<UsersController> sutProvider,
        Guid userId)
    {
        var user = new User
        {
            Id = userId,
            PublicKey = "publicKey",
            SignedPublicKeyOwnershipClaim = "signature"
        };

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(userId).Returns(user);
        sutProvider.GetDependency<IUserSigningKeysRepository>().GetByUserIdAsync(userId).Returns(new SigningKeyData()
        {
            KeyAlgorithm = SigningKeyType.Ed25519,
            WrappedSigningKey = "signingKey",
            VerifyingKey = "verifyingKey"
        });

        var result = await sutProvider.Sut.GetAccountKeys(userId.ToString());
        Assert.NotNull(result);
        Assert.Equal("publicKey", result.PublicKey);
        Assert.Equal("verifyingKey", result.VerifyingKey);
    }

    [Theory]
    [BitAutoData]
    public async Task GetAccountKeys_ReturnsPublicUserKeysResponseModel_WithNullVerifyingKey(
        SutProvider<UsersController> sutProvider,
        Guid userId)
    {
        var user = new User
        {
            Id = userId,
            PublicKey = "publicKey",
            SignedPublicKeyOwnershipClaim = "signature"
        };

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(userId).Returns(user);
        sutProvider.GetDependency<IUserSigningKeysRepository>().GetByUserIdAsync(userId).ReturnsNull();

        var result = await sutProvider.Sut.GetAccountKeys(userId.ToString());
        Assert.NotNull(result);
        Assert.Equal("publicKey", result.PublicKey);
        Assert.Null(result.VerifyingKey);
    }
}
