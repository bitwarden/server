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
public class UpdateOrganizationReportDataFileStorageCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task GetUploadUrlAsync_WithMismatchedFileId_ShouldThrowNotFoundException(
        SutProvider<UpdateOrganizationReportDataFileStorageCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportDataRequest>();
        var existingReport = fixture.Build<OrganizationReport>()
            .With(x => x.Id, request.ReportId)
            .With(x => x.OrganizationId, request.OrganizationId)
            .With(x => x.FileId, "stored-file-id")
            .Create();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.ReportId)
            .Returns(existingReport);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.GetUploadUrlAsync(request, "attacker-supplied-file-id"));

        Assert.Equal("Report not found", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task GetUploadUrlAsync_WithNonExistentReport_ShouldThrowNotFoundException(
        SutProvider<UpdateOrganizationReportDataFileStorageCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportDataRequest>();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.ReportId)
            .Returns((OrganizationReport)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.GetUploadUrlAsync(request, "any-file-id"));

        Assert.Equal("Report not found", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task GetUploadUrlAsync_WithMismatchedOrgId_ShouldThrowNotFoundException(
        SutProvider<UpdateOrganizationReportDataFileStorageCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var request = fixture.Create<UpdateOrganizationReportDataRequest>();
        var existingReport = fixture.Build<OrganizationReport>()
            .With(x => x.Id, request.ReportId)
            .With(x => x.OrganizationId, Guid.NewGuid()) // Different org ID
            .Create();

        sutProvider.GetDependency<IOrganizationReportRepository>()
            .GetByIdAsync(request.ReportId)
            .Returns(existingReport);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await sutProvider.Sut.GetUploadUrlAsync(request, "any-file-id"));
    }
}
