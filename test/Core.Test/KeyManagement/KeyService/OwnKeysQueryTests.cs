using Bit.Core.Entities;
using Bit.Core.KeyManagement.Enums;
using Bit.Core.KeyManagement.KeyService;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.KeyService;

[SutProviderCustomize]
public class OwnKeysQueryTests
{
    private const string RootKeyId = "b3e70000000000000000000000000003";
    private const string PublicKeyId = "c47b0000000000000000000000000001";
    private const string VerifyingKeyId = "e2100000000000000000000000000002";
    private static readonly KeyFilter _all = new(null, null);

    [Theory, BitAutoData]
    public async Task NeverRotated_AddsMarker(SutProvider<OwnKeysQuery> sutProvider, User user)
    {
        Arrange(sutProvider, user);
        user.LastKeyRotationDate = null;

        var keys = await sutProvider.Sut.Run(user, _all);

        var privateKey = Assert.Single(keys, key => key.Type == KeyResourceTypes.PrivateKey);
        Assert.Equal(PublicKeyId, privateKey.KeyId);
        Assert.Equal(RootKeyId, privateKey.SealedByKeyId);
        Assert.Contains(user.PrivateKey!, privateKey.Data);

        var marker = Assert.Single(keys, key => key.Type == KeyResourceTypes.SymmetricKey);
        Assert.Equal(KeyIds.HistoryMarker, marker.KeyId);
        Assert.Equal(RootKeyId, marker.SealedByKeyId);
        Assert.Equal(KeyPurposes.UserEncryptionRoot, marker.KeyPurpose);
        Assert.Equal("", marker.Data);
    }

    [Theory, BitAutoData]
    public async Task Rotated_OmitsMarker(SutProvider<OwnKeysQuery> sutProvider, User user)
    {
        Arrange(sutProvider, user);
        user.LastKeyRotationDate = DateTime.UtcNow;

        var keys = await sutProvider.Sut.Run(user, _all);

        Assert.DoesNotContain(keys, key => key.Type == KeyResourceTypes.SymmetricKey);
    }

    [Theory, BitAutoData]
    public async Task NoRootKeyId_OmitsMarker(SutProvider<OwnKeysQuery> sutProvider, User user)
    {
        Arrange(sutProvider, user);
        user.UserKeyId = null;
        user.LastKeyRotationDate = null;

        var keys = await sutProvider.Sut.Run(user, _all);

        var key = Assert.Single(keys);
        Assert.Null(key.SealedByKeyId);
    }

    [Theory, BitAutoData]
    public async Task V2User_AddsSigningKey(SutProvider<OwnKeysQuery> sutProvider, User user)
    {
        Arrange(sutProvider, user);
        user.SecurityVersion = SecurityVersions.V2;
        sutProvider.GetDependency<IUserSignatureKeyPairRepository>().GetByUserIdAsync(user.Id)
            .Returns(new SignatureKeyPairData(SignatureAlgorithm.Ed25519, "wrappedSigningKey", "verifyingKey"));

        var keys = await sutProvider.Sut.Run(user, _all);

        var signingKey = Assert.Single(keys, key => key.Type == KeyResourceTypes.SigningKey);
        Assert.Equal(VerifyingKeyId, signingKey.KeyId);
        Assert.Equal(KeyPurposes.UserSigningRoot, signingKey.KeyPurpose);
        Assert.Equal(RootKeyId, signingKey.SealedByKeyId);
        Assert.Equal(
            "{\"type\":\"legacy-compat-signing-key-envelope\",\"wrappedKey\":\"wrappedSigningKey\",\"publicKey\":\"verifyingKey\"}",
            signingKey.Data);
    }

    [Theory, BitAutoData]
    public async Task PurposeFilter_Narrows(SutProvider<OwnKeysQuery> sutProvider, User user)
    {
        Arrange(sutProvider, user);
        user.LastKeyRotationDate = null;

        var keys = await sutProvider.Sut.Run(user, new KeyFilter([KeyPurposes.UserEncryptionRoot], null));

        var key = Assert.Single(keys);
        Assert.Equal(KeyIds.HistoryMarker, key.KeyId);
    }

    private static void Arrange(SutProvider<OwnKeysQuery> sutProvider, User user)
    {
        user.UserKeyId = RootKeyId;
        user.SecurityVersion = null;

        var keyIdReader = sutProvider.GetDependency<IKeyIdReader>();
        keyIdReader.ReadPublicKeyId(Arg.Any<string>()).Returns(PublicKeyId);
        keyIdReader.ReadVerifyingKeyId(Arg.Any<string>()).Returns(VerifyingKeyId);
    }
}
