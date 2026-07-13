using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Repositories;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class GetOrganizationInviteLinkStatusQueryTests
{
    [Theory, BitAutoData]
    public async Task GetStatusAsync_WithValidInput_Success(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        organization.Seats = null;
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, organization.Id, 0);

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsSuccess);
        var status = result.AsSuccess;
        Assert.Equal(organization.Name, status.OrganizationName);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task GetStatusAsync_SupportsConfirmation(
        bool supportsConfirmation,
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        // Arrange
        organization.Seats = null;
        inviteLink.OrganizationId = organization.Id;
        inviteLink.SupportsConfirmation = supportsConfirmation;

        SetupMocks(sutProvider, code, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, organization.Id, 0);

        // Act
        var result = await sutProvider.Sut.GetStatusAsync(code);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(supportsConfirmation, result.AsSuccess.SupportsConfirmation);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_InviteLinkNotFound_ReturnsNotFoundError(
        Guid code,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(code).ReturnsNull();

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_OrganizationNotFound_ReturnsNotFoundError(
        Guid code,
        OrganizationInviteLink inviteLink,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(code).Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(inviteLink.OrganizationId).ReturnsNull();

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_OrganizationDisabled_ReturnsNotFoundError(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        organization.Enabled = false;
        inviteLink.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(code).Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id).Returns(organization);

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_UseInviteLinksFalse_ReturnsNotAvailableError(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        organization.UseInviteLinks = false;

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetOccupiedSeatCountByOrganizationIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_NoSeatLimit_ReturnsSeatsAvailableTrue(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        organization.Seats = null;
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, organization.Id, 50);

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsSuccess);
        Assert.True(result.AsSuccess.SeatsAvailable);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_UnusedSeats_ReturnsSeatsAvailableTrue(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        organization.Seats = 10;
        organization.MaxAutoscaleSeats = null;
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, organization.Id, 5);

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsSuccess);
        Assert.True(result.AsSuccess.SeatsAvailable);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_AutoscaleHeadroom_ReturnsSeatsAvailableTrue(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        organization.Seats = 10;
        organization.MaxAutoscaleSeats = 20;
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, organization.Id, 10);

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsSuccess);
        Assert.True(result.AsSuccess.SeatsAvailable);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_NoSeatsAndNoHeadroom_ReturnsSeatsAvailableFalseAndSsoNull(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        organization.Seats = 10;
        organization.MaxAutoscaleSeats = 10;
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, organization.Id, 10);

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsSuccess);
        Assert.False(result.AsSuccess.SeatsAvailable);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_SeatsUnavailable_ReturnsSsoNullWithoutCheckingSsoConfig(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SsoConfig ssoConfig,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        organization.Seats = 10;
        organization.MaxAutoscaleSeats = 10;
        organization.Identifier = "my-org";
        organization.UseSso = true;
        ssoConfig.Enabled = true;
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, organization.Id, 10);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(organization.Id).Returns(ssoConfig);

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsSuccess);
        Assert.Null(result.AsSuccess.Sso);
        await sutProvider.GetDependency<ISsoConfigRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_OccupancyAtAutoscaleCeiling_ReturnsSeatsAvailableFalse(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        // Occupancy has autoscaled past the base seats up to the ceiling.
        organization.Seats = 5;
        organization.MaxAutoscaleSeats = 10;
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, organization.Id, 10);

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsSuccess);
        Assert.False(result.AsSuccess.SeatsAvailable);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_NoAutoscaleCap_ReturnsSeatsAvailableTrue(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        organization.Seats = 10;
        organization.MaxAutoscaleSeats = null;
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, organization.Id, 10);

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsSuccess);
        Assert.True(result.AsSuccess.SeatsAvailable);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_NoSsoConfig_ReturnsSsoNull(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        organization.Seats = null;
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, organization.Id, 0);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(organization.Id).ReturnsNull();

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsSuccess);
        Assert.Null(result.AsSuccess.Sso);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_SsoConfigDisabled_ReturnsSsoNull(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SsoConfig ssoConfig,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        organization.Seats = null;
        ssoConfig.Enabled = false;
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, organization.Id, 0);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(organization.Id).Returns(ssoConfig);

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsSuccess);
        Assert.Null(result.AsSuccess.Sso);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_NoOrganizationIdentifier_ReturnsSsoNull(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SsoConfig ssoConfig,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        organization.Seats = null;
        organization.Identifier = null;
        ssoConfig.Enabled = true;
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, organization.Id, 0);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(organization.Id).Returns(ssoConfig);

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsSuccess);
        Assert.Null(result.AsSuccess.Sso);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_WithSsoConfiguredAndRequireSsoPolicyEnabled_ReturnsSsoRequiredTrue(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SsoConfig ssoConfig,
        Policy requireSsoPolicy,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        organization.Seats = null;
        organization.Identifier = "my-org";
        organization.UseSso = true;
        organization.UsePolicies = true;
        ssoConfig.Enabled = true;
        requireSsoPolicy.Enabled = true;
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, organization.Id, 0);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(organization.Id).Returns(ssoConfig);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.RequireSso).Returns(requireSsoPolicy);

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.AsSuccess.Sso);
        Assert.Equal("my-org", result.AsSuccess.Sso.OrgSsoId);
        Assert.True(result.AsSuccess.Sso.Required);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_WithSsoConfiguredAndNoRequireSsoPolicy_ReturnsSsoRequiredFalse(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SsoConfig ssoConfig,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        organization.Seats = null;
        organization.Identifier = "my-org";
        organization.UseSso = true;
        ssoConfig.Enabled = true;
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, organization.Id, 0);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(organization.Id).Returns(ssoConfig);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.RequireSso).ReturnsNull();

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.AsSuccess.Sso);
        Assert.Equal("my-org", result.AsSuccess.Sso.OrgSsoId);
        Assert.False(result.AsSuccess.Sso.Required);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_WithUseSsoFalse_ReturnsSsoNull(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SsoConfig ssoConfig,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        organization.Seats = null;
        organization.UseSso = false;
        ssoConfig.Enabled = true;
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, organization.Id, 0);

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsSuccess);
        Assert.Null(result.AsSuccess.Sso);
        await sutProvider.GetDependency<ISsoConfigRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_WithSsoEnabledAndUsePoliciesFalse_ReturnsSsoRequiredFalse(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SsoConfig ssoConfig,
        Policy requireSsoPolicy,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        organization.Seats = null;
        organization.Identifier = "my-org";
        organization.UseSso = true;
        organization.UsePolicies = false;
        ssoConfig.Enabled = true;
        requireSsoPolicy.Enabled = true;
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, organization.Id, 0);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(organization.Id).Returns(ssoConfig);

        var result = await sutProvider.Sut.GetStatusAsync(code);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.AsSuccess.Sso);
        Assert.False(result.AsSuccess.Sso.Required);
        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdTypeAsync(default, default);
    }

    private static void SetupMocks(
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider,
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization)
    {
        organization.Enabled = true;
        organization.UseInviteLinks = true;
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(code)
            .Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
    }

    private static void SetupOccupiedSeats(
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider,
        Guid organizationId,
        int users)
    {
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organizationId)
            .Returns(new OrganizationSeatCounts { Users = users });
    }
}
