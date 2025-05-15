using AutoFixture;
using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.ReportFeatures;
using Bit.Core.Tools.ReportFeatures.Requests;
using Bit.Core.Tools.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.ReportFeatures;

[SutProviderCustomize]
public class DeletePasswordHealthReportApplicationCommandTests
{
    [Theory, BitAutoData]
    public async Task DropPasswordHealthReportApplicationAsync_withValidRequest_Success(
        SutProvider<DropPasswordHealthReportApplicationCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var passwordHealthReportApplications = fixture.CreateMany<PasswordHealthReportApplication>(2).ToList();
        // only take one id from the list - we only want to drop one record
        var request = fixture.Build<DropPasswordHealthReportApplicationRequest>()
                        .With(x => x.PasswordHealthReportApplicationIds,
                            passwordHealthReportApplications.Select(x => x.Id).Take(1).ToList())
                        .Create();

        sutProvider.GetDependency<IPasswordHealthReportApplicationRepository>()
            .GetByOrganizationIdAsync(Arg.Any<Guid>())
            .Returns(passwordHealthReportApplications);

        // Act
        await sutProvider.Sut.DropPasswordHealthReportApplicationAsync(request);

        // Assert
        await sutProvider.GetDependency<IPasswordHealthReportApplicationRepository>()
            .Received(1)
            .GetByOrganizationIdAsync(request.OrganizationId);

        await sutProvider.GetDependency<IPasswordHealthReportApplicationRepository>()
            .Received(1)
            .DeleteAsync(Arg.Is<PasswordHealthReportApplication>(_ =>
                request.PasswordHealthReportApplicationIds.Contains(_.Id)));
    }

    [Theory, BitAutoData]
    public async Task DropPasswordHealthReportApplicationAsync_withValidRequest_nothingToDrop(
        SutProvider<DropPasswordHealthReportApplicationCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        var passwordHealthReportApplications = fixture.CreateMany<PasswordHealthReportApplication>(2).ToList();
        // we are passing invalid data
        var request = fixture.Build<DropPasswordHealthReportApplicationRequest>()
                .With(x => x.PasswordHealthReportApplicationIds, new List<Guid> { Guid.NewGuid() })
                        .Create();

        sutProvider.GetDependency<IPasswordHealthReportApplicationRepository>()
            .GetByOrganizationIdAsync(Arg.Any<Guid>())
            .Returns(passwordHealthReportApplications);

        // Act
        await sutProvider.Sut.DropPasswordHealthReportApplicationAsync(request);

        // Assert
        await sutProvider.GetDependency<IPasswordHealthReportApplicationRepository>()
            .Received(1)
            .GetByOrganizationIdAsync(request.OrganizationId);

        await sutProvider.GetDependency<IPasswordHealthReportApplicationRepository>()
            .Received(0)
            .DeleteAsync(Arg.Any<PasswordHealthReportApplication>());
    }

    [Theory, BitAutoData]
    public async Task DropPasswordHealthReportApplicationAsync_withNodata_fails(
        SutProvider<DropPasswordHealthReportApplicationCommand> sutProvider)
    {
        // Arrange
        var fixture = new Fixture();
        // we are passing invalid data
        var request = fixture.Build<DropPasswordHealthReportApplicationRequest>()
                .Create();

        sutProvider.GetDependency<IPasswordHealthReportApplicationRepository>()
            .GetByOrganizationIdAsync(Arg.Any<Guid>())
            .Returns(null as List<PasswordHealthReportApplication>);

        // Act
        await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.DropPasswordHealthReportApplicationAsync(request));

        // Assert
        await sutProvider.GetDependency<IPasswordHealthReportApplicationRepository>()
            .Received(1)
            .GetByOrganizationIdAsync(request.OrganizationId);

        await sutProvider.GetDependency<IPasswordHealthReportApplicationRepository>()
            .Received(0)
            .DeleteAsync(Arg.Any<PasswordHealthReportApplication>());
    }
}
