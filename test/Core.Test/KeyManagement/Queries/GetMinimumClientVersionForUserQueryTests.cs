using Bit.Core.Entities;
using Bit.Core.KeyManagement.Queries;
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
            SecurityVersion = 2
        });
        Assert.Equal(Core.KeyManagement.Constants.MinimumClientVersionForV2Encryption, version);
    }

    [Fact]
    public async Task Run_ReturnsNull_ForV1User()
    {
        var sut = new GetMinimumClientVersionForUserQuery();
        var version = await sut.Run(new User
        {
            SecurityVersion = 1
        });
        Assert.Null(version);
    }
}


