using AutoFixture;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Api.Dirt.Controllers;
using Bit.Api.Dirt.Models;
using Bit.Api.Dirt.Models.Response;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.OrganizationReportMembers.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.OrganizationAuthorization;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Dirt;


[ControllerCustomize(typeof(ReportsController))]
[SutProviderCustomize]
public class ReportsControllerTests
{
    // GetMemberCipherDetails

    [Theory, BitAutoData]
    public async Task GetMemberCipherDetails_withAccess_success(
        SutProvider<ReportsController> sutProvider,
        Guid orgId)
    {
        // Arrange
        SetupAuthorization(sutProvider);

        var details = new List<RiskInsightsReportDetail>
        {
            new()
            {
                UserGuid = Guid.NewGuid(),
                UserName = "Test User",
                Email = "user@example.com",
                UsesKeyConnector = false,
                CipherIds = ["cipher-1", "cipher-2"]
            }
        };
        sutProvider.GetDependency<IRiskInsightsReportQuery>()
            .GetRiskInsightsReportDetails(Arg.Is<RiskInsightsReportRequest>(_ => _.OrganizationId == orgId))
            .Returns(details);

        // Act
        var result = (await sutProvider.Sut.GetMemberCipherDetails(orgId)).ToList();

        // Assert
        var response = Assert.Single(result);
        Assert.Equal("user@example.com", response.Email);
        Assert.Equal(["cipher-1", "cipher-2"], response.CipherIds);
    }

    [Theory, BitAutoData]
    public async Task GetMemberCipherDetails_withoutAccess_throwsNotFound(
        SutProvider<ReportsController> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetMemberCipherDetails(orgId));

        await sutProvider.GetDependency<IRiskInsightsReportQuery>()
            .DidNotReceive()
            .GetRiskInsightsReportDetails(Arg.Any<RiskInsightsReportRequest>());
    }

    [Theory, BitAutoData]
    public async Task GetMemberCipherDetails_withoutUseRiskInsights_throwsBadRequest(
        SutProvider<ReportsController> sutProvider,
        Guid orgId)
    {
        // Arrange
        SetupAuthorization(sutProvider, useRiskInsights: false);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.GetMemberCipherDetails(orgId));

        await sutProvider.GetDependency<IRiskInsightsReportQuery>()
            .DidNotReceive()
            .GetRiskInsightsReportDetails(Arg.Any<RiskInsightsReportRequest>());
    }

    [Theory, BitAutoData]
    public async Task GetMemberCipherDetails_withoutOrganizationAbility_throwsBadRequest(
        SutProvider<ReportsController> sutProvider,
        Guid orgId)
    {
        // Arrange
        SetupMissingOrganizationAbility(sutProvider);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.GetMemberCipherDetails(orgId));

        await sutProvider.GetDependency<IRiskInsightsReportQuery>()
            .DidNotReceive()
            .GetRiskInsightsReportDetails(Arg.Any<RiskInsightsReportRequest>());
    }

    // GetMemberAdoptionReport

    [Theory, BitAutoData]
    public async Task GetMemberAdoptionReportAsync_ReturnsCountsAndMembers(
        SutProvider<ReportsController> sutProvider,
        Guid organizationId,
        Guid organizationUserId,
        Guid userId)
    {
        // Arrange
        SetupAuthorization(sutProvider);

        var report = new MemberAdoptionReportResult
        {
            TotalMemberCount = 3,
            ActiveMemberCount = 2,
            InactiveMemberCount = 1,
            SponsoredFamiliesRedeemedCount = 1,
            Members =
            [
                new MemberAdoptionReportMember
                {
                    OrganizationUserId = organizationUserId,
                    UserId = userId,
                    Name = "Test User",
                    Email = "user@example.com",
                    HasRecentLogin = true,
                    HasExtensionInstalled = true,
                    VaultItemCount = 12,
                    SharedItemCount = 3
                }
            ]
        };
        sutProvider.GetDependency<IMemberAdoptionReportQuery>()
            .GetMemberAdoptionReportAsync(Arg.Is<MemberAdoptionReportRequest>(r => r.OrganizationId == organizationId))
            .Returns(report);

        // Act
        var result = await sutProvider.Sut.GetMemberAdoptionReportAsync(organizationId);

        // Assert
        var response = Assert.IsType<MemberAdoptionReportResponseModel>(result.Value);
        Assert.Equal(3, response.TotalMemberCount);
        Assert.Equal(2, response.ActiveMemberCount);
        Assert.Equal(1, response.InactiveMemberCount);
        Assert.Equal(1, response.SponsoredFamiliesRedeemedCount);

        var member = Assert.Single(response.Members);
        Assert.Equal(organizationUserId, member.OrganizationUserId);
        Assert.Equal(userId, member.UserId);
        Assert.Equal("Test User", member.Name);
        Assert.Equal("user@example.com", member.Email);
        Assert.True(member.HasRecentLogin);
        Assert.True(member.HasExtensionInstalled);
        Assert.Equal(12, member.VaultItemCount);
        Assert.Equal(3, member.SharedItemCount);
    }

    [Theory, BitAutoData]
    public async Task GetMemberAdoptionReportAsync_PassesRouteOrganizationIdToQuery(
        SutProvider<ReportsController> sutProvider,
        Guid organizationId)
    {
        // Arrange
        SetupAuthorization(sutProvider);

        sutProvider.GetDependency<IMemberAdoptionReportQuery>()
            .GetMemberAdoptionReportAsync(Arg.Any<MemberAdoptionReportRequest>())
            .Returns(new MemberAdoptionReportResult());

        // Act
        await sutProvider.Sut.GetMemberAdoptionReportAsync(organizationId);

        // Assert
        await sutProvider.GetDependency<IMemberAdoptionReportQuery>()
            .Received(1)
            .GetMemberAdoptionReportAsync(Arg.Is<MemberAdoptionReportRequest>(r => r.OrganizationId == organizationId));
    }

    [Theory, BitAutoData]
    public async Task GetMemberAdoptionReportAsync_OrganizationWithNoMembers_ReturnsEmptyReport(
        SutProvider<ReportsController> sutProvider,
        Guid organizationId)
    {
        // Arrange
        SetupAuthorization(sutProvider);

        sutProvider.GetDependency<IMemberAdoptionReportQuery>()
            .GetMemberAdoptionReportAsync(Arg.Any<MemberAdoptionReportRequest>())
            .Returns(new MemberAdoptionReportResult());

        // Act
        var result = await sutProvider.Sut.GetMemberAdoptionReportAsync(organizationId);

        // Assert
        var response = Assert.IsType<MemberAdoptionReportResponseModel>(result.Value);
        Assert.Equal(0, response.TotalMemberCount);
        Assert.Equal(0, response.ActiveMemberCount);
        Assert.Equal(0, response.InactiveMemberCount);
        Assert.Equal(0, response.SponsoredFamiliesRedeemedCount);
        Assert.Empty(response.Members);
    }

    [Theory, BitAutoData]
    public async Task GetMemberAdoptionReportAsync_withoutUseRiskInsights_throwsBadRequest(
        SutProvider<ReportsController> sutProvider,
        Guid organizationId)
    {
        // Arrange
        SetupAuthorization(sutProvider, useRiskInsights: false);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetMemberAdoptionReportAsync(organizationId));

        await sutProvider.GetDependency<IMemberAdoptionReportQuery>()
            .DidNotReceive()
            .GetMemberAdoptionReportAsync(Arg.Any<MemberAdoptionReportRequest>());
    }

    [Theory, BitAutoData]
    public async Task GetMemberAdoptionReportAsync_withoutOrganizationAbility_throwsBadRequest(
        SutProvider<ReportsController> sutProvider,
        Guid organizationId)
    {
        // Arrange
        SetupMissingOrganizationAbility(sutProvider);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetMemberAdoptionReportAsync(organizationId));

        await sutProvider.GetDependency<IMemberAdoptionReportQuery>()
            .DidNotReceive()
            .GetMemberAdoptionReportAsync(Arg.Any<MemberAdoptionReportRequest>());
    }

    [Fact]
    public void GetMemberAdoptionReportAsync_RequiresAccessReportsAuthorization()
    {
        var action = typeof(ReportsController)
            .GetMethod(nameof(ReportsController.GetMemberAdoptionReportAsync))!;

        Assert.Contains(
            action.GetCustomAttributes(inherit: true),
            attribute => attribute is AuthorizeAttribute<AccessReportsRequirement>);
    }

    // GetPasswordHealthReportApplications

    [Theory, BitAutoData]
    public async Task GetPasswordHealthReportApplicationAsync_Success(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        SetupAuthorization(sutProvider);

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
        await sutProvider.GetDependency<IGetPasswordHealthReportApplicationQuery>()
            .DidNotReceive()
            .GetPasswordHealthReportApplicationAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetPasswordHealthReportApplicationAsync_withoutUseRiskInsights_throwsBadRequest(
        SutProvider<ReportsController> sutProvider,
        Guid orgId)
    {
        // Arrange
        SetupAuthorization(sutProvider, useRiskInsights: false);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetPasswordHealthReportApplications(orgId));

        await sutProvider.GetDependency<IGetPasswordHealthReportApplicationQuery>()
            .DidNotReceive()
            .GetPasswordHealthReportApplicationAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task GetPasswordHealthReportApplicationAsync_withoutOrganizationAbility_throwsBadRequest(
        SutProvider<ReportsController> sutProvider,
        Guid orgId)
    {
        // Arrange
        SetupMissingOrganizationAbility(sutProvider);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetPasswordHealthReportApplications(orgId));

        await sutProvider.GetDependency<IGetPasswordHealthReportApplicationQuery>()
            .DidNotReceive()
            .GetPasswordHealthReportApplicationAsync(Arg.Any<Guid>());
    }


    // AddPasswordHealthReportApplications 

    [Theory, BitAutoData]
    public async Task AddPasswordHealthReportApplicationAsync_multiple_withAccess_success(
        SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        SetupAuthorization(sutProvider);

        // Act
        var fixture = new Fixture();
        var request = fixture.CreateMany<PasswordHealthReportApplicationModel>(2);
        await sutProvider.Sut.AddPasswordHealthReportApplications(request);

        // Assert
        _ = sutProvider.GetDependency<IAddPasswordHealthReportApplicationCommand>()
            .Received(1)
            .AddPasswordHealthReportApplicationAsync(Arg.Any<IEnumerable<AddPasswordHealthReportApplicationRequest>>());
    }

    [Theory, BitAutoData]
    public async Task AddPasswordHealthReportApplicationAsync_multiple_withoutAccess_throwsNotFound(
        SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(false);

        // Act & Assert
        var fixture = new Fixture();
        var request = fixture.CreateMany<PasswordHealthReportApplicationModel>(2);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.AddPasswordHealthReportApplications(request));

        await sutProvider.GetDependency<IAddPasswordHealthReportApplicationCommand>()
            .DidNotReceive()
            .AddPasswordHealthReportApplicationAsync(Arg.Any<IEnumerable<AddPasswordHealthReportApplicationRequest>>());
    }

    [Theory, BitAutoData]
    public async Task AddPasswordHealthReportApplicationAsync_multiple_withoutUseRiskInsights_throwsBadRequest(
        SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        SetupAuthorization(sutProvider, useRiskInsights: false);

        // Act & Assert
        var fixture = new Fixture();
        var request = fixture.CreateMany<PasswordHealthReportApplicationModel>(2);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AddPasswordHealthReportApplications(request));

        await sutProvider.GetDependency<IAddPasswordHealthReportApplicationCommand>()
            .DidNotReceive()
            .AddPasswordHealthReportApplicationAsync(Arg.Any<IEnumerable<AddPasswordHealthReportApplicationRequest>>());
    }

    [Theory, BitAutoData]
    public async Task AddPasswordHealthReportApplicationAsync_multiple_oneOrgWithoutUseRiskInsights_throwsBadRequest(
        SutProvider<ReportsController> sutProvider)
    {
        // Arrange: every org in the batch is authorized except the second one, which lacks the ability
        SetupAuthorization(sutProvider);

        var fixture = new Fixture();
        var request = fixture.CreateMany<PasswordHealthReportApplicationModel>(2).ToList();

        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(request[1].OrganizationId)
            .Returns(new OrganizationAbility { UseRiskInsights = false });

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AddPasswordHealthReportApplications(request));

        await sutProvider.GetDependency<IAddPasswordHealthReportApplicationCommand>()
            .DidNotReceive()
            .AddPasswordHealthReportApplicationAsync(Arg.Any<IEnumerable<AddPasswordHealthReportApplicationRequest>>());
    }

    // DropPasswordHealthReportApplication

    [Theory, BitAutoData]
    public async Task DropPasswordHealthReportApplicationAsync_withoutAccess(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().AccessReports(Arg.Any<Guid>()).Returns(false);

        // Act
        var fixture = new Fixture();
        var request = fixture.Create<DropPasswordHealthReportApplicationRequest>();
        await Assert.ThrowsAsync<NotFoundException>(async () =>
                await sutProvider.Sut.DropPasswordHealthReportApplication(request));

        // Assert
        await sutProvider.GetDependency<IDropPasswordHealthReportApplicationCommand>()
            .DidNotReceive()
            .DropPasswordHealthReportApplicationAsync(Arg.Any<DropPasswordHealthReportApplicationRequest>());
    }

    [Theory, BitAutoData]
    public async Task DropPasswordHealthReportApplicationAsync_withoutUseRiskInsights_throwsBadRequest(
        SutProvider<ReportsController> sutProvider,
        DropPasswordHealthReportApplicationRequest request)
    {
        // Arrange
        SetupAuthorization(sutProvider, useRiskInsights: false);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.DropPasswordHealthReportApplication(request));

        await sutProvider.GetDependency<IDropPasswordHealthReportApplicationCommand>()
            .DidNotReceive()
            .DropPasswordHealthReportApplicationAsync(Arg.Any<DropPasswordHealthReportApplicationRequest>());
    }

    [Theory, BitAutoData]
    public async Task DropPasswordHealthReportApplicationAsync_withoutOrganizationAbility_throwsBadRequest(
        SutProvider<ReportsController> sutProvider,
        DropPasswordHealthReportApplicationRequest request)
    {
        // Arrange
        SetupMissingOrganizationAbility(sutProvider);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.DropPasswordHealthReportApplication(request));

        await sutProvider.GetDependency<IDropPasswordHealthReportApplicationCommand>()
            .DidNotReceive()
            .DropPasswordHealthReportApplicationAsync(Arg.Any<DropPasswordHealthReportApplicationRequest>());
    }

    [Theory, BitAutoData]
    public async Task DropPasswordHealthReportApplicationAsync_withAccess_success(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        SetupAuthorization(sutProvider);

        // Act
        var fixture = new Fixture();
        var request = fixture.Create<DropPasswordHealthReportApplicationRequest>();
        await sutProvider.Sut.DropPasswordHealthReportApplication(request);

        // Assert
        _ = sutProvider.GetDependency<IDropPasswordHealthReportApplicationCommand>()
            .Received(1)
            .DropPasswordHealthReportApplicationAsync(Arg.Is<DropPasswordHealthReportApplicationRequest>(_ =>
                _.OrganizationId == request.OrganizationId &&
                _.PasswordHealthReportApplicationIds == request.PasswordHealthReportApplicationIds));
    }

    // GetPasskeyDirectory - not organization scoped, so it is not gated on UseRiskInsights

    [Theory, BitAutoData]
    public async Task GetPasskeyDirectory_ReturnsExpectedEntries(SutProvider<ReportsController> sutProvider)
    {
        // Arrange
        var entries = new List<PasskeyDirectoryEntry>
        {
            new() { DomainName = "example.com", Passwordless = true, Mfa = false, Instructions = "https://example.com/help" },
            new() { DomainName = "test.com", Passwordless = false, Mfa = true, Instructions = "" }
        };
        sutProvider.GetDependency<IGetPasskeyDirectoryQuery>()
            .GetPasskeyDirectoryAsync()
            .Returns(entries);

        // Act
        var result = (await sutProvider.Sut.GetPasskeyDirectoryAsync()).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("example.com", result[0].DomainName);
        Assert.True(result[0].Passwordless);
        Assert.False(result[0].Mfa);
        Assert.Equal("https://example.com/help", result[0].Instructions);
        Assert.Equal("test.com", result[1].DomainName);
        Assert.False(result[1].Passwordless);
        Assert.True(result[1].Mfa);
    }

    private static void SetupAuthorization(
        SutProvider<ReportsController> sutProvider,
        bool useRiskInsights = true)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(Arg.Any<Guid>())
            .Returns(true);

        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(Arg.Any<Guid>())
            .Returns(new OrganizationAbility { UseRiskInsights = useRiskInsights });
    }

    private static void SetupMissingOrganizationAbility(SutProvider<ReportsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .AccessReports(Arg.Any<Guid>())
            .Returns(true);

        sutProvider.GetDependency<IOrganizationAbilityCacheService>()
            .GetOrganizationAbilityAsync(Arg.Any<Guid>())
            .Returns((OrganizationAbility?)null);
    }
}
