using System.Text.Json;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.KeyService;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.KeyService;

[SutProviderCustomize]
public class UnlockMethodsQueryTests
{
    private const string RootKeyId = "b3e70000000000000000000000000003";

    [Theory, BitAutoData]
    public async Task MasterPassword_ReturnsKdf(SutProvider<UnlockMethodsQuery> sutProvider, User user)
    {
        ArrangeMasterPassword(user);
        user.Kdf = KdfType.Argon2id;
        user.KdfIterations = 3;
        user.KdfMemory = 64;
        user.KdfParallelism = 4;
        user.MasterPasswordSalt = "alice@example.com";

        var methods = await sutProvider.Sut.Run(user, [UnlockMethodKinds.MasterPassword]);

        var method = Assert.Single(methods);
        Assert.Equal(user.Id, method.Id);
        Assert.Equal(RootKeyId, method.ContainedKeyId);
        Assert.Equal(
            "{\"type\":\"raw\",\"wrappedKey\":\"wrappedUserKey\",\"kdf\":{\"type\":\"argon2id\",\"iterations\":3,\"memory\":64,\"parallelism\":4},\"salt\":\"alice@example.com\"}",
            method.Data);
    }

    [Theory, BitAutoData]
    public async Task Pbkdf2_OmitsArgon2Params(SutProvider<UnlockMethodsQuery> sutProvider, User user)
    {
        ArrangeMasterPassword(user);
        user.Kdf = KdfType.PBKDF2_SHA256;
        user.KdfIterations = 600000;

        var methods = await sutProvider.Sut.Run(user, [UnlockMethodKinds.MasterPassword]);

        using var data = JsonDocument.Parse(Assert.Single(methods).Data);
        var kdf = data.RootElement.GetProperty("kdf");
        Assert.Equal("pbkdf2", kdf.GetProperty("type").GetString());
        Assert.Equal(600000, kdf.GetProperty("iterations").GetInt32());
        Assert.False(kdf.TryGetProperty("memory", out _));
    }

    [Theory, BitAutoData]
    public async Task KeyConnector_NamesOrg(SutProvider<UnlockMethodsQuery> sutProvider, User user,
        OrganizationUserOrganizationDetails membership)
    {
        ArrangeMasterPassword(user);
        user.UsesKeyConnector = true;
        membership.UseKeyConnector = true;
        membership.SsoConfig = new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.KeyConnector,
            KeyConnectorUrl = "https://key-connector.example.com",
        }.Serialize();
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByUserAsync(user.Id)
            .Returns([membership]);

        var methods = await sutProvider.Sut.Run(user, null);

        var method = Assert.Single(methods);
        Assert.Equal(UnlockMethodKinds.KeyConnector, method.Kind);
        Assert.Equal(membership.OrganizationId, method.Organization);
        Assert.Equal("{\"wrappedKey\":\"wrappedUserKey\"}", method.Data);
    }

    [Theory, BitAutoData]
    public async Task Devices_OnlyTrusted(SutProvider<UnlockMethodsQuery> sutProvider, User user,
        Device trusted, Device untrusted)
    {
        user.UserKeyId = RootKeyId;
        trusted.EncryptedUserKey = "userKey";
        trusted.EncryptedPrivateKey = "privateKey";
        trusted.EncryptedPublicKey = "publicKey";
        untrusted.EncryptedUserKey = null;
        sutProvider.GetDependency<IDeviceRepository>().GetManyByUserIdAsync(user.Id).Returns([trusted, untrusted]);

        var methods = await sutProvider.Sut.Run(user, [UnlockMethodKinds.Device]);

        var method = Assert.Single(methods);
        Assert.Equal(trusted.Id, method.Id);
        Assert.Equal(trusted.Id, method.Device);
        Assert.Equal(RootKeyId, method.ContainedKeyId);
        Assert.Equal(
            "{\"wrappedKey\":\"userKey\",\"wrappedPrivateKey\":\"privateKey\",\"wrappedPublicKey\":\"publicKey\"}",
            method.Data);
    }

    [Theory, BitAutoData]
    public async Task Passkeys_OnlyWithPrf(SutProvider<UnlockMethodsQuery> sutProvider, User user,
        WebAuthnCredential withPrf, WebAuthnCredential withoutPrf)
    {
        withoutPrf.EncryptedUserKey = null!;
        sutProvider.GetDependency<IWebAuthnCredentialRepository>().GetManyByUserIdAsync(user.Id)
            .Returns([withPrf, withoutPrf]);

        var methods = await sutProvider.Sut.Run(user, [UnlockMethodKinds.Passkey]);

        var method = Assert.Single(methods);
        Assert.Equal(withPrf.Id, method.Passkey);
    }

    [Theory, BitAutoData]
    public async Task KindFilter_SkipsReads(SutProvider<UnlockMethodsQuery> sutProvider, User user)
    {
        ArrangeMasterPassword(user);

        await sutProvider.Sut.Run(user, [UnlockMethodKinds.MasterPassword]);

        await sutProvider.GetDependency<IDeviceRepository>().DidNotReceiveWithAnyArgs().GetManyByUserIdAsync(default);
        await sutProvider.GetDependency<IWebAuthnCredentialRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByUserIdAsync(default);
    }

    private static void ArrangeMasterPassword(User user)
    {
        user.Key = "wrappedUserKey";
        user.MasterPassword = "hash";
        user.UsesKeyConnector = false;
        user.UserKeyId = RootKeyId;
    }
}
