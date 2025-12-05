using Bit.Core.Entities;
using Bit.Core.KeyManagement.Queries;
using Bit.Test.Common.Constants;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Queries;

public class GetMinimumClientVersionForUserQueryTests
{
    [Fact]
    public async Task Run_ReturnsMinVersion_ForV2User()
    {
        var sut = new GetMinimumClientVersionForUserQuery();
        var version = await sut.Run(new User
        {
            SecurityVersion = 2,
            PrivateKey = TestEncryptionConstants.V2PrivateKey,
        });
        Assert.Equal(Core.KeyManagement.Constants.MinimumClientVersionForV2Encryption, version);
    }

    [Fact]
    public async Task Run_ReturnsNull_ForV1User()
    {
        var sut = new GetMinimumClientVersionForUserQuery();
        var version = await sut.Run(new User
        {
            SecurityVersion = 1,
            PrivateKey = TestEncryptionConstants.V1EncryptedBase64,
        });
        Assert.Null(version);
    }

    [Fact]
    public async Task Run_ReturnsNull_ForSecurityVersion1ButPrivateKeyV2User()
    {
        var sut = new GetMinimumClientVersionForUserQuery();
        var version = await sut.Run(new User
        {
            SecurityVersion = 1,
            PrivateKey = TestEncryptionConstants.V2PrivateKey,
        });
        Assert.Null(version);
    }

    [Fact]
    public async Task Run_ReturnsNull_ForPrivateKeyV1ButSecurityVersion2User()
    {
        var sut = new GetMinimumClientVersionForUserQuery();
        var version = await sut.Run(new User
        {
            SecurityVersion = 2,
            PrivateKey = TestEncryptionConstants.V1EncryptedBase64,
        });
        Assert.Null(version);
    }


    [Fact]
    public async Task Run_ReturnsNull_ForV1UserWithNull()
    {
        var sut = new GetMinimumClientVersionForUserQuery();
        var version = await sut.Run(new User
        {
            SecurityVersion = null,
            PrivateKey = TestEncryptionConstants.V2PrivateKey,
        });
        Assert.Null(version);
    }
}


