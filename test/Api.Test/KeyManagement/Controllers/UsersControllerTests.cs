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
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetPublicKey(new Guid().ToString()));
    }

    [Theory]
    [BitAutoData]
    public async Task GetAccountKeys_UserNotFound_ThrowsNotFoundException(
        SutProvider<UsersController> sutProvider)
    {
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(Arg.Any<Guid>()).ReturnsNull();
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAccountKeys(new Guid().ToString()));
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
            SignedPublicKey = "signedPublicKey",
        };

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(userId).Returns(user);
        sutProvider.GetDependency<IUserSignatureKeyPairRepository>().GetByUserIdAsync(userId).Returns(new SignatureKeyPairData
        {
            WrappedSigningKey = "signingKey",
            VerifyingKey = "verifyingKey",
            SignatureAlgorithm = SignatureAlgorithm.Ed25519
        });

        var result = await sutProvider.Sut.GetAccountKeys(userId.ToString());
        Assert.NotNull(result);
        Assert.Equal("publicKey", result.PublicKey);
        Assert.Equal("signedPublicKey", result.SignedPublicKey);
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
            SignedPublicKey = null,
        };

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(userId).Returns(user);
        sutProvider.GetDependency<IUserSignatureKeyPairRepository>().GetByUserIdAsync(userId).ReturnsNull();

        var result = await sutProvider.Sut.GetAccountKeys(userId.ToString());
        Assert.NotNull(result);
        Assert.Equal("publicKey", result.PublicKey);
        Assert.Null(result.SignedPublicKey);
        Assert.Null(result.VerifyingKey);
    }
}
