using Bit.Api.Tools.Controllers;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Tools.ReportFeatures.Interfaces;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Tools.Controllers;


[ControllerCustomize(typeof(ReportsController))]
[SutProviderCustomize]
public class ReportsControllerTests
{
    [Theory, BitAutoData]
    public async Task GetPasswordHealthReportApplicationAsync_Success(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(true);

        // Act
        var orgId = Guid.NewGuid();
        var result = await sutProvider.Sut.GetPasswordHealthReportApplications(orgId);

        // Assert
        _ = sutProvider.GetDependency<IGetPasswordHealthReportApplicationQuery>()
            .Received(1)
            .GetPasswordHealthReportApplicationAsync(Arg.Is<Guid>(_ => _ == orgId));
    }

    [Theory, BitAutoData]
    public async Task GetPasswordHealthReportApplicationAsync_withoutAccess(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(false);

        // Act & Assert
        var orgId = Guid.NewGuid();
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetPasswordHealthReportApplications(orgId));

        // Assert
        _ = sutProvider.GetDependency<IGetPasswordHealthReportApplicationQuery>()
            .Received(0);
    }


}
