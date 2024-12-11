using Bit.Api.Auth.Models.Request.WebAuthn;
using Bit.Api.KeyManagement.Validators;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Validators;

[SutProviderCustomize]
public class WebAuthnLoginKeyRotationValidatorTests
{
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WrongWebAuthnKeys_Throws(
        SutProvider<WebAuthnLoginKeyRotationValidator> sutProvider,
        User user,
        IEnumerable<WebAuthnLoginRotateKeyRequestModel> webauthnRotateCredentialData
    )
    {
        var webauthnKeysToRotate = webauthnRotateCredentialData
            .Select(e => new WebAuthnLoginRotateKeyRequestModel
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                EncryptedPublicKey = e.EncryptedPublicKey,
                EncryptedUserKey = e.EncryptedUserKey,
            })
            .ToList();

        var data = new WebAuthnCredential
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            EncryptedPublicKey = "TestKey",
            EncryptedUserKey = "Test",
        };
        sutProvider
            .GetDependency<IWebAuthnCredentialRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<WebAuthnCredential> { data });

        await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.ValidateAsync(user, webauthnKeysToRotate)
        );
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_NullUserKey_Throws(
        SutProvider<WebAuthnLoginKeyRotationValidator> sutProvider,
        User user,
        IEnumerable<WebAuthnLoginRotateKeyRequestModel> webauthnRotateCredentialData
    )
    {
        var guid = Guid.NewGuid();
        var webauthnKeysToRotate = webauthnRotateCredentialData
            .Select(e => new WebAuthnLoginRotateKeyRequestModel
            {
                Id = guid,
                EncryptedPublicKey = e.EncryptedPublicKey,
            })
            .ToList();

        var data = new WebAuthnCredential
        {
            Id = guid,
            EncryptedPublicKey = "TestKey",
            EncryptedUserKey = "Test",
        };
        sutProvider
            .GetDependency<IWebAuthnCredentialRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<WebAuthnCredential> { data });

        await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.ValidateAsync(user, webauthnKeysToRotate)
        );
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_NullPublicKey_Throws(
        SutProvider<WebAuthnLoginKeyRotationValidator> sutProvider,
        User user,
        IEnumerable<WebAuthnLoginRotateKeyRequestModel> webauthnRotateCredentialData
    )
    {
        var guid = Guid.NewGuid();
        var webauthnKeysToRotate = webauthnRotateCredentialData
            .Select(e => new WebAuthnLoginRotateKeyRequestModel
            {
                Id = guid,
                EncryptedUserKey = e.EncryptedUserKey,
            })
            .ToList();

        var data = new WebAuthnCredential
        {
            Id = guid,
            EncryptedPublicKey = "TestKey",
            EncryptedUserKey = "Test",
        };
        sutProvider
            .GetDependency<IWebAuthnCredentialRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<WebAuthnCredential> { data });

        await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.ValidateAsync(user, webauthnKeysToRotate)
        );
    }
}
