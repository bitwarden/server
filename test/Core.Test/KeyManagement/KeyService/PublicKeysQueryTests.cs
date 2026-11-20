using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Enums;
using Bit.Core.KeyManagement.KeyService;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.KeyService;

[SutProviderCustomize]
public class PublicKeysQueryTests
{
    private const string PublicKeyId = "c47b0000000000000000000000000001";
    private const string VerifyingKeyId = "e2100000000000000000000000000002";
    private static readonly KeyFilter _all = new(null, null);

    [Theory, BitAutoData]
    public async Task V1User_ReturnsSharingKey(SutProvider<PublicKeysQuery> sutProvider, User user)
    {
        user.SecurityVersion = null;
        Arrange(sutProvider, user);

        var keys = await sutProvider.Sut.Run(KeyOwnerType.User, [user.Id], _all);

        var key = Assert.Single(keys);
        Assert.Equal(new KeyOwner(KeyOwnerType.User, user.Id), key.Owner);
        Assert.Equal(KeyPurposes.UserSharingRoot, key.KeyPurpose);
        Assert.Equal(PublicKeyId, key.KeyId);
        Assert.Equal(KeyIds.ToResourceId(PublicKeyId), key.Id);
        Assert.Contains(user.PublicKey!, key.Data);
        await sutProvider.GetDependency<IUserSignatureKeyPairRepository>().DidNotReceiveWithAnyArgs()
            .GetByUserIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task V2User_AddsVerifyingKey(SutProvider<PublicKeysQuery> sutProvider, User user)
    {
        user.SecurityVersion = SecurityVersions.V2;
        Arrange(sutProvider, user);
        sutProvider.GetDependency<IUserSignatureKeyPairRepository>().GetByUserIdAsync(user.Id)
            .Returns(new SignatureKeyPairData(SignatureAlgorithm.Ed25519, "wrappedSigningKey", "verifyingKey"));

        var keys = await sutProvider.Sut.Run(KeyOwnerType.User, [user.Id], _all);

        Assert.Equal(2, keys.Count);
        var verifyingKey = Assert.Single(keys, key => key.KeyPurpose == KeyPurposes.UserSigningRoot);
        Assert.Equal(VerifyingKeyId, verifyingKey.KeyId);
        Assert.Equal("{\"type\":\"verifying-key\",\"publicKey\":\"verifyingKey\"}", verifyingKey.Data);
    }

    [Theory, BitAutoData]
    public async Task UserWithoutKeys_ReturnsNone(SutProvider<PublicKeysQuery> sutProvider, User user)
    {
        user.PublicKey = null;
        Arrange(sutProvider, user);

        var keys = await sutProvider.Sut.Run(KeyOwnerType.User, [user.Id], _all);

        Assert.Empty(keys);
    }

    [Theory, BitAutoData]
    public async Task KeyIdFilter_Narrows(SutProvider<PublicKeysQuery> sutProvider, User user)
    {
        user.SecurityVersion = null;
        Arrange(sutProvider, user);

        var keys = await sutProvider.Sut.Run(KeyOwnerType.User, [user.Id], new KeyFilter(null, ["other"]));

        Assert.Empty(keys);
    }

    [Theory, BitAutoData]
    public async Task Organization_ReturnsKey(SutProvider<PublicKeysQuery> sutProvider,
        Organization organization)
    {
        organization.PublicKey = "orgPublicKey";
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyByIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(organization.Id)))
            .Returns([organization]);
        sutProvider.GetDependency<IKeyIdReader>().ReadPublicKeyId("orgPublicKey").Returns(PublicKeyId);

        var keys = await sutProvider.Sut.Run(KeyOwnerType.Organization, [organization.Id], _all);

        var key = Assert.Single(keys);
        Assert.Equal(KeyPurposes.OrganizationSharingRoot, key.KeyPurpose);
        Assert.Equal(organization.CreationDate, key.CreatedAt);
        Assert.Equal("{\"type\":\"public-key\",\"publicKey\":\"orgPublicKey\"}", key.Data);
    }

    [Theory, BitAutoData]
    public async Task Provider_ReturnsNone(SutProvider<PublicKeysQuery> sutProvider, Guid providerId)
    {
        var keys = await sutProvider.Sut.Run(KeyOwnerType.Provider, [providerId], _all);

        Assert.Empty(keys);
    }

    private static void Arrange(SutProvider<PublicKeysQuery> sutProvider, User user)
    {
        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user.Id)))
            .Returns([user]);

        var keyIdReader = sutProvider.GetDependency<IKeyIdReader>();
        keyIdReader.ReadPublicKeyId(Arg.Any<string>()).Returns(PublicKeyId);
        keyIdReader.ReadVerifyingKeyId(Arg.Any<string>()).Returns(VerifyingKeyId);
    }
}
