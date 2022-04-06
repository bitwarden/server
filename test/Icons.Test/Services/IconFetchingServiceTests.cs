using System.Threading.Tasks;
using Bit.Icons.Services;
using Bit.Test.Common.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Bit.Icons.Test.Services
{
    public class IconFetchingServiceTests
    {
        [CiSkippedTheory]
        [InlineData("www.google.com")] // https site
        [InlineData("neverssl.com")] // http site
        [InlineData("tdameritrade.com")]
        [InlineData("icloud.com")]
        [InlineData("bofa.com")]
        public async Task GetIconAsync_Success(string domain)
        {
            var sut = new IconFetchingService(GetLogger());
            var result = await sut.GetIconAsync(domain);

            Assert.NotNull(result);
            Assert.NotNull(result.Icon);
        }

        [Theory]
        [InlineData("1.1.1.1")]
        [InlineData("")]
        [InlineData("localhost")]
        public async Task GetIconAsync_ReturnsNull(string domain)
        {
            var sut = new IconFetchingService(GetLogger());
            var result = await sut.GetIconAsync(domain);

            Assert.Null(result);
        }

        private static ILogger<IconFetchingService> GetLogger()
        {
            var services = new ServiceCollection();
            services.AddLogging(b =>
            {
                b.ClearProviders();
                b.AddDebug();
            });

            var provider = services.BuildServiceProvider();

            return provider.GetRequiredService<ILogger<IconFetchingService>>();
        }
    }
}
