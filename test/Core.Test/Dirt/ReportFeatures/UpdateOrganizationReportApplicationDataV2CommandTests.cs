using AutoFixture;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.ReportFeatures;

[SutProviderCustomize]
public class UpdateOrganizationReportApplicationDataV2CommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateApplicationDataAsync_ValidRequest_ReturnsUpdatedReport(
        SutProvider<UpdateOrganizationReportApplicationDataV2Command> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var organizationId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var applicationData = "test-application-data";

        var existingReport = fixture.Build<OrganizationReport>()
            .With(r => r.Id, reportId)
            .With(r => r.OrganizationId, organizationId)
            .Create();

        var updatedReport = fixture.Build<OrganizationReport>()
            .With(r => r.Id, reportId)
            .With(r => r.OrganizationId, organizationId)
            .With(r => r.ApplicationData, applicationData)
            .Create();

        var request = new UpdateOrganizationReportApplicationDataRequest
        {
            Id = reportId,
            OrganizationId = organizationId,
            ApplicationData = applicationData
        };

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns(existingReport);

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .UpdateApplicationDataAsync(organizationId, reportId, applicationData)
            .Returns(updatedReport);

        // Act
        var result = await sutProvider.Sut.UpdateApplicationDataAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(applicationData, result.ApplicationData);

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .Received(1)
            .UpdateApplicationDataAsync(organizationId, reportId, applicationData);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateApplicationDataAsync_ReportNotFound_ThrowsNotFoundException(
        SutProvider<UpdateOrganizationReportApplicationDataV2Command> sutProvider)
    {
        // Arrange
        var request = new UpdateOrganizationReportApplicationDataRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            ApplicationData = "test-data"
        };

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.Id)
            .Returns(null as OrganizationReport);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await sutProvider.Sut.UpdateApplicationDataAsync(request));
        Assert.Equal("Organization report not found", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateApplicationDataAsync_OrganizationMismatch_ThrowsNotFoundException(
        SutProvider<UpdateOrganizationReportApplicationDataV2Command> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var reportId = Guid.NewGuid();
        var requestOrgId = Guid.NewGuid();
        var differentOrgId = Guid.NewGuid();

        var existingReport = fixture.Build<OrganizationReport>()
            .With(r => r.Id, reportId)
            .With(r => r.OrganizationId, differentOrgId)
            .Create();

        var request = new UpdateOrganizationReportApplicationDataRequest
        {
            Id = reportId,
            OrganizationId = requestOrgId,
            ApplicationData = "test-data"
        };

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(reportId)
            .Returns(existingReport);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await sutProvider.Sut.UpdateApplicationDataAsync(request));
        Assert.Equal("Organization report not found", exception.Message);

        await sutProvider.GetDependency<IOrganizationReportRepository>()
            .DidNotReceive()
            .UpdateApplicationDataAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>());
    }
}
