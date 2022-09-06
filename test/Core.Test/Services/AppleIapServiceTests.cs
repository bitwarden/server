using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.Core;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class AppleIapServiceTests
{
    [Theory, BitAutoData]
    public async Task GetReceiptStatusAsync_MoreThanFourAttempts_Throws(SutProvider<AppleIapService> sutProvider)
    {
        var result = await sutProvider.Sut.GetReceiptStatusAsync("test", false, 5, null);
        Assert.Null(result);

        var errorLog = sutProvider.GetDependency<ILogger<AppleIapService>>()
            .ReceivedCalls()
            .SingleOrDefault(LogOneWarning);

        Assert.True(errorLog != null, "Must contain one error log of warning level containing 'null'");

        static bool LogOneWarning(ICall call)
        {
            if (call.GetMethodInfo().Name != "Log")
            {
                return false;
            }

            var args = call.GetArguments();
            var logLevel = (LogLevel)args[0];
            var exception = (Exception)args[3];

            return logLevel == LogLevel.Warning && exception.Message.Contains("null");
        }
    }
}
