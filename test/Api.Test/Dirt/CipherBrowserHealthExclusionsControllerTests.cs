using System.Security.Claims;
using Bit.Api.Dirt.Controllers;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Dirt;

[ControllerCustomize(typeof(CipherBrowserHealthExclusionsController))]
[SutProviderCustomize]
public class CipherBrowserHealthExclusionsControllerTests
{
    [Theory, BitAutoData]
    public async Task PutExcludeFromBrowserHealth_CallsCommandWithCallingUser(
        SutProvider<CipherBrowserHealthExclusionsController> sutProvider, Guid cipherId, Guid userId)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);

        await sutProvider.Sut.PutExcludeFromBrowserHealthAsync(cipherId);

        await sutProvider.GetDependency<IExcludeCipherFromBrowserHealthCommand>().Received(1).ExcludeAsync(cipherId, userId);
    }
}
