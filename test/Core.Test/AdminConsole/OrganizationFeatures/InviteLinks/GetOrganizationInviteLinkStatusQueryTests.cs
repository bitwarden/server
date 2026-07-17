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
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Seats = null;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, inviteLink.OrganizationId, 0);

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        var status = result.AsSuccess;
        Assert.Equal(organization.Name, status.OrganizationName);
        Assert.True(status.LinksEnabled);
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
        Guid organizationId,
        Guid code,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId).ReturnsNull();

        var result = await sutProvider.Sut.GetStatusAsync(organizationId, code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_CodeMismatch_ReturnsNotFoundError(
        OrganizationInviteLink inviteLink,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(inviteLink);

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, Guid.NewGuid());

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_OrganizationNotFound_ReturnsNotFoundError(
        OrganizationInviteLink inviteLink,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        inviteLink.Code = code.ToString();

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(inviteLink.OrganizationId).ReturnsNull();

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_OrganizationDisabled_ReturnsNotFoundError(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Enabled = false;
        inviteLink.Code = code.ToString();

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(inviteLink.OrganizationId).Returns(organization);

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_UseInviteLinksFalse_ReturnsLinksDisabled(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        organization.UseInviteLinks = false;

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        var status = result.AsSuccess;
        Assert.Equal(organization.Name, status.OrganizationName);
        Assert.False(status.LinksEnabled);
        Assert.False(status.SeatsAvailable);
        Assert.Null(status.Sso);

        // When links are disabled we short-circuit without touching seats or SSO.
        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetOccupiedSeatCountByOrganizationIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_NoSeatLimit_ReturnsSeatsAvailableTrue(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Seats = null;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, inviteLink.OrganizationId, 50);

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        Assert.True(result.AsSuccess.SeatsAvailable);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_UnusedSeats_ReturnsSeatsAvailableTrue(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Seats = 10;
        organization.MaxAutoscaleSeats = null;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, inviteLink.OrganizationId, 5);

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        Assert.True(result.AsSuccess.SeatsAvailable);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_AutoscaleHeadroom_ReturnsSeatsAvailableTrue(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Seats = 10;
        organization.MaxAutoscaleSeats = 20;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, inviteLink.OrganizationId, 10);

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        Assert.True(result.AsSuccess.SeatsAvailable);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_NoSeatsAndNoHeadroom_ReturnsSeatsAvailableFalseAndSsoNull(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Seats = 10;
        organization.MaxAutoscaleSeats = 10;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, inviteLink.OrganizationId, 10);

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        Assert.False(result.AsSuccess.SeatsAvailable);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_SeatsUnavailable_ReturnsSsoNullWithoutCheckingSsoConfig(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SsoConfig ssoConfig,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Seats = 10;
        organization.MaxAutoscaleSeats = 10;
        organization.Identifier = "my-org";
        organization.UseSso = true;
        ssoConfig.Enabled = true;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, inviteLink.OrganizationId, 10);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(ssoConfig);

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        Assert.Null(result.AsSuccess.Sso);
        await sutProvider.GetDependency<ISsoConfigRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_OccupancyAtAutoscaleCeiling_ReturnsSeatsAvailableFalse(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Seats = 5;
        organization.MaxAutoscaleSeats = 10;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, inviteLink.OrganizationId, 10);

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        Assert.False(result.AsSuccess.SeatsAvailable);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_NoAutoscaleCap_ReturnsSeatsAvailableTrue(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Seats = 10;
        organization.MaxAutoscaleSeats = null;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, inviteLink.OrganizationId, 10);

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        Assert.True(result.AsSuccess.SeatsAvailable);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_NoSsoConfig_ReturnsSsoNull(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Seats = null;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, inviteLink.OrganizationId, 0);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).ReturnsNull();

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        Assert.Null(result.AsSuccess.Sso);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_SsoConfigDisabled_ReturnsSsoNull(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SsoConfig ssoConfig,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Seats = null;
        ssoConfig.Enabled = false;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, inviteLink.OrganizationId, 0);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(ssoConfig);

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        Assert.Null(result.AsSuccess.Sso);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_NoOrganizationIdentifier_ReturnsSsoNull(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SsoConfig ssoConfig,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Seats = null;
        organization.Identifier = null;
        ssoConfig.Enabled = true;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, inviteLink.OrganizationId, 0);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(ssoConfig);

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        Assert.Null(result.AsSuccess.Sso);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_WithSsoConfiguredAndRequireSsoPolicyEnabled_ReturnsSsoRequiredTrue(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SsoConfig ssoConfig,
        Policy requireSsoPolicy,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Seats = null;
        organization.Identifier = "my-org";
        organization.UseSso = true;
        organization.UsePolicies = true;
        ssoConfig.Enabled = true;
        requireSsoPolicy.Enabled = true;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, inviteLink.OrganizationId, 0);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(ssoConfig);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(inviteLink.OrganizationId, PolicyType.RequireSso).Returns(requireSsoPolicy);

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.AsSuccess.Sso);
        Assert.Equal("my-org", result.AsSuccess.Sso.OrgSsoId);
        Assert.True(result.AsSuccess.Sso.Required);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_WithSsoConfiguredAndNoRequireSsoPolicy_ReturnsSsoRequiredFalse(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SsoConfig ssoConfig,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Seats = null;
        organization.Identifier = "my-org";
        organization.UseSso = true;
        ssoConfig.Enabled = true;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, inviteLink.OrganizationId, 0);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(ssoConfig);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(inviteLink.OrganizationId, PolicyType.RequireSso).ReturnsNull();

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.AsSuccess.Sso);
        Assert.Equal("my-org", result.AsSuccess.Sso.OrgSsoId);
        Assert.False(result.AsSuccess.Sso.Required);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_WithUseSsoFalse_ReturnsSsoNull(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SsoConfig ssoConfig,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Seats = null;
        organization.UseSso = false;
        ssoConfig.Enabled = true;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, inviteLink.OrganizationId, 0);

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        Assert.Null(result.AsSuccess.Sso);
        await sutProvider.GetDependency<ISsoConfigRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetStatusAsync_WithSsoEnabledAndUsePoliciesFalse_ReturnsSsoRequiredFalse(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SsoConfig ssoConfig,
        Policy requireSsoPolicy,
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Seats = null;
        organization.Identifier = "my-org";
        organization.UseSso = true;
        organization.UsePolicies = false;
        ssoConfig.Enabled = true;
        requireSsoPolicy.Enabled = true;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        SetupOccupiedSeats(sutProvider, inviteLink.OrganizationId, 0);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(ssoConfig);

        var result = await sutProvider.Sut.GetStatusAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.AsSuccess.Sso);
        Assert.False(result.AsSuccess.Sso.Required);
        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdTypeAsync(default, default);
    }

    private static void SetupMocks(
        SutProvider<GetOrganizationInviteLinkStatusQuery> sutProvider,
        OrganizationInviteLink inviteLink,
        Organization organization)
    {
        organization.Enabled = true;
        organization.UseInviteLinks = true;
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId)
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
