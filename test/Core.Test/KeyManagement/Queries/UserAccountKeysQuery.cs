using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Queries;
using Bit.Core.KeyManagement.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Queries;

[SutProviderCustomize]
public class UserAccountKeysQueryTests
{
    [Theory, BitAutoData]
    public async Task V1User_Success(SutProvider<UserAccountKeysQuery> sutProvider, User user)
    {
        var result = await sutProvider.Sut.Run(user);
        Assert.Equal(user.GetPublicKeyEncryptionKeyPair(), result.PublicKeyEncryptionKeyPairData);
    }

    [Theory, BitAutoData]
    public async Task V2User_Success(SutProvider<UserAccountKeysQuery> sutProvider, User user)
    {
        user.SecurityState = "v2";
        user.SecurityVersion = 2;
        var signatureKeyPairRepository = sutProvider.GetDependency<IUserSignatureKeyPairRepository>();
        signatureKeyPairRepository.GetByUserIdAsync(user.Id).Returns(new SignatureKeyPairData(Core.KeyManagement.Enums.SignatureAlgorithm.Ed25519, "wrappedSigningKey", "verifyingKey"));
        var result = await sutProvider.Sut.Run(user);
        Assert.Equal(user.GetPublicKeyEncryptionKeyPair(), result.PublicKeyEncryptionKeyPairData);
        Assert.NotNull(result.SignatureKeyPairData);
        Assert.Equal(user.SecurityState, result.SecurityStateData.SecurityState);
        Assert.Equal(user.GetSecurityVersion(), result.SecurityStateData.SecurityVersion);
        Assert.Equal(user.SecurityVersion, result.SecurityStateData.SecurityVersion);
        Assert.Equal(user.SecurityState, result.SecurityStateData.SecurityState);
    }

}
