using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Bit.Core.Exceptions;
using Bit.Core.Settings;
using Bit.Core.Tools.Queries;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.Tools.Queries;

[SutProviderCustomize]
public class GetInactiveTwoFactorQueryTests
{
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Send(request, cancellationToken);

        public virtual Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }

    [Theory]
    [BitAutoData]
    public async Task GetInactiveTwoFactor_FromApi_Success(SutProvider<GetInactiveTwoFactorQuery> sutProvider)
    {
        sutProvider.GetDependency<IDistributedCache>().Get(Arg.Any<string>()).ReturnsNull();

        var handler = Substitute.ForPartsOf<MockHttpMessageHandler>();
        handler.Send(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}", Encoding.UTF8, MediaTypeNames.Application.Json)
            });

        var client = new HttpClient(handler);
        sutProvider.GetDependency<IHttpClientFactory>()
            .CreateClient()
            .Returns(client);

        sutProvider.GetDependency<IGlobalSettings>().TwoFactorDirectory.Returns(
            new GlobalSettings.TwoFactorDirectorySettings()
            {
                CacheExpirationHours = 1,
                Uri = new Uri("http://localhost")
            });

        await sutProvider.Sut.GetInactiveTwoFactorAsync();

        await sutProvider.GetDependency<IDistributedCache>().Received(1).SetAsync(Arg.Any<string>(),
            Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetInactiveTwoFactor_FromApi_Failure(SutProvider<GetInactiveTwoFactorQuery> sutProvider)
    {
        sutProvider.GetDependency<IDistributedCache>().Get(Arg.Any<string>()).ReturnsNull();

        var handler = Substitute.ForPartsOf<MockHttpMessageHandler>();
        handler.Send(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.Unauthorized
            });

        var client = new HttpClient(handler);
        sutProvider.GetDependency<IHttpClientFactory>()
            .CreateClient()
            .Returns(client);

        sutProvider.GetDependency<IGlobalSettings>().TwoFactorDirectory.Returns(
            new GlobalSettings.TwoFactorDirectorySettings()
            {
                CacheExpirationHours = 1,
                Uri = new Uri("http://localhost")
            });

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.GetInactiveTwoFactorAsync());
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
