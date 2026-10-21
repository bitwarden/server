using System.Security.Claims;
using Bit.Api.Dirt.Controllers;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Dirt;

[ControllerCustomize(typeof(CipherReportExclusionsController))]
[SutProviderCustomize]
public class CipherReportExclusionsControllerTests
{
    [Theory, BitAutoData]
    public async Task PutExcludeFromReports_CallsCommandWithCallingUser(
        SutProvider<CipherReportExclusionsController> sutProvider, Guid cipherId, Guid userId)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);

        await sutProvider.Sut.PutExcludeFromReportsAsync(cipherId);

        await sutProvider.GetDependency<IExcludeCipherFromReportsCommand>().Received(1).ExcludeAsync(cipherId, userId);
    }
}
