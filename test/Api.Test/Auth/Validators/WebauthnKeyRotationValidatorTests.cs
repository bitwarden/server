using Bit.Api.Auth.Validators;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Auth.Validators;

[SutProviderCustomize]
public class WebauthnKeyRotationValidatorTests
{
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WrongWebauthnKeys_Throws(
        SutProvider<WebauthnKeyRotationValidator> sutProvider, User user,
        IEnumerable<WebauthnRotateKeyData> webauthnRotateCredentialData)
    {
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);
        var webauthnKeysToRotate = webauthnRotateCredentialData.Select(e => new WebauthnRotateKeyData
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            EncryptedPublicKey = e.EncryptedPublicKey,
            EncryptedUserKey = e.EncryptedUserKey
        }).ToList();

        var data = new WebAuthnCredential
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            EncryptedPublicKey = "TestKey",
            EncryptedUserKey = "Test"
        };
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetManyByUserIdAsync(user.Id).Returns(new List<WebAuthnCredential> { data });

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user, webauthnKeysToRotate));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_NullUserKey_Throws(
        SutProvider<WebauthnKeyRotationValidator> sutProvider, User user,
        IEnumerable<WebauthnRotateKeyData> webauthnRotateCredentialData)
    {
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);
        var guid = Guid.NewGuid();
        var webauthnKeysToRotate = webauthnRotateCredentialData.Select(e => new WebauthnRotateKeyData
        {
            Id = guid,
            EncryptedPublicKey = e.EncryptedPublicKey,
        }).ToList();

        var data = new WebAuthnCredential
        {
            Id = guid,
            EncryptedPublicKey = "TestKey",
            EncryptedUserKey = "Test"
        };
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetManyByUserIdAsync(user.Id).Returns(new List<WebAuthnCredential> { data });

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user, webauthnKeysToRotate));
    }


    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_NullPublicKey_Throws(
        SutProvider<WebauthnKeyRotationValidator> sutProvider, User user,
        IEnumerable<WebauthnRotateKeyData> webauthnRotateCredentialData)
    {
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);
        var guid = Guid.NewGuid();
        var webauthnKeysToRotate = webauthnRotateCredentialData.Select(e => new WebauthnRotateKeyData
        {
            Id = guid,
            EncryptedUserKey = e.EncryptedUserKey,
        }).ToList();

        var data = new WebAuthnCredential
        {
            Id = guid,
            EncryptedPublicKey = "TestKey",
            EncryptedUserKey = "Test"
        };
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetManyByUserIdAsync(user.Id).Returns(new List<WebAuthnCredential> { data });

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user, webauthnKeysToRotate));
    }

}
