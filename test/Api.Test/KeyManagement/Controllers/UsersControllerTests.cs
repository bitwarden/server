#nullable enable
using Bit.Api.KeyManagement.Controllers;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Queries.Interfaces;
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
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetPublicKeyAsync(new Guid()));
    }

    [Theory]
    [BitAutoData]
    public async Task GetPublicKey_ReturnsUserKeyResponseModel(
        SutProvider<UsersController> sutProvider,
        Guid userId)
    {
        var publicKey = "publicKey";
        sutProvider.GetDependency<IUserRepository>().GetPublicKeyAsync(userId).Returns(publicKey);

        var result = await sutProvider.Sut.GetPublicKeyAsync(userId);
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(publicKey, result.PublicKey);
    }

    [Theory]
    [BitAutoData]
    public async Task GetAccountKeys_UserNotFound_ThrowsNotFoundException(
        SutProvider<UsersController> sutProvider)
    {
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(Arg.Any<Guid>()).ReturnsNull();
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetAccountKeysAsync(new Guid()));
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
        sutProvider.GetDependency<IUserAccountKeysQuery>()
            .Run(user)
            .Returns(new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData("wrappedPrivateKey", "publicKey", "signedPublicKey"),
                SignatureKeyPairData = new SignatureKeyPairData(SignatureAlgorithm.Ed25519, "wrappedSigningKey", "verifyingKey"),
            });

        var result = await sutProvider.Sut.GetAccountKeysAsync(userId);
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
        sutProvider.GetDependency<IUserAccountKeysQuery>()
            .Run(user)
            .Returns(new UserAccountKeysData
            {
                PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData("wrappedPrivateKey", "publicKey", null),
                SignatureKeyPairData = null,
            });

        var result = await sutProvider.Sut.GetAccountKeysAsync(userId);
        Assert.NotNull(result);
        Assert.Equal("publicKey", result.PublicKey);
        Assert.Null(result.SignedPublicKey);
        Assert.Null(result.VerifyingKey);
    }
}
