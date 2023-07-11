using Bit.Core.Tools.Queries;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;
using System.Text.Json;
using NSubstitute.ReturnsExtensions;

namespace Bit.Core.Test.Tools.Queries;

[SutProviderCustomize]
public class GetInactiveTwoFactorQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetInactiveTwoFactor_FromApi_Success(SutProvider<GetInactiveTwoFactorQuery> sutProvider)
    {
        // Cache retrieval returns null
        sutProvider.GetDependency<IDistributedCache>().Get(Arg.Any<string>()).ReturnsNull();
        //sutProvider.GetDependency<IHttpClient>()
        
        await sutProvider.Sut.GetInactiveTwoFactorAsync();
        
        // Will return cached values - Should not hit the save method
        await sutProvider.GetDependency<IDistributedCache>().DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetInactiveTwoFactor_FromCache_Success(Dictionary<string, string> dictionary,
        SutProvider<GetInactiveTwoFactorQuery> sutProvider)
    {
        // Byte array needs to deserialize into dictionary object
        var bytes = JsonSerializer.SerializeToUtf8Bytes(dictionary);
        sutProvider.GetDependency<IDistributedCache>().Get(Arg.Any<string>()).Returns(bytes);

        await sutProvider.Sut.GetInactiveTwoFactorAsync();

        await sutProvider.GetDependency<IDistributedCache>().DidNotReceive().SetAsync(Arg.Any<string>(),
            Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }
}
