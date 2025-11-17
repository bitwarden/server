using Bit.Core.Entities;
using Bit.Core.KeyManagement.Queries;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Queries;

public class GetMinimumClientVersionForUserQueryTests
{
    private class FakeIsV2Query : IIsV2EncryptionUserQuery
    {
        private readonly bool _isV2;
        public FakeIsV2Query(bool isV2) { _isV2 = isV2; }
        public Task<bool> Run(User user) => Task.FromResult(_isV2);
    }

    [Fact]
    public async Task Run_ReturnsMinVersion_ForV2User()
    {
        var sut = new GetMinimumClientVersionForUserQuery(new FakeIsV2Query(true));
        var version = await sut.Run(new User());
        Assert.Equal(Core.KeyManagement.Constants.MinimumClientVersion, version);
    }

    [Fact]
    public async Task Run_ReturnsNull_ForV1User()
    {
        var sut = new GetMinimumClientVersionForUserQuery(new FakeIsV2Query(false));
        var version = await sut.Run(new User());
        Assert.Null(version);
    }
}


