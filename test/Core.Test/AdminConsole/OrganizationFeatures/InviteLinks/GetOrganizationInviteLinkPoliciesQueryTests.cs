using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class GetOrganizationInviteLinkPoliciesQueryTests
{
    [Theory, BitAutoData]
    public async Task GetPoliciesAsync_WithValidInput_ReturnsMasterPasswordAndResetPasswordOnly(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkPoliciesQuery> sutProvider)
    {
        organization.UsePolicies = true;
        inviteLink.OrganizationId = organization.Id;

        List<Policy> policies =
        [
            new Policy { Type = PolicyType.MasterPassword, Enabled = true },
            new Policy { Type = PolicyType.ResetPassword, Enabled = true },
            new Policy { Type = PolicyType.RequireSso, Enabled = false },
            new Policy { Type = PolicyType.SingleOrg, Enabled = true },
        ];

        SetupMocks(sutProvider, code, inviteLink, organization);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(organization.Id)
            .Returns(policies);

        var result = await sutProvider.Sut.GetPoliciesAsync(code);

        Assert.True(result.IsSuccess);
        var returned = result.AsSuccess.ToList();
        Assert.Equal(2, returned.Count);
        Assert.Contains(returned, p => p.Type == PolicyType.MasterPassword);
        Assert.Contains(returned, p => p.Type == PolicyType.ResetPassword);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesAsync_InviteLinkNotFound_ReturnsNotFoundError(
        Guid code,
        SutProvider<GetOrganizationInviteLinkPoliciesQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(code).ReturnsNull();

        var result = await sutProvider.Sut.GetPoliciesAsync(code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesAsync_OrganizationNotFound_ReturnsNotFoundError(
        Guid code,
        OrganizationInviteLink inviteLink,
        SutProvider<GetOrganizationInviteLinkPoliciesQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(code).Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(inviteLink.OrganizationId).ReturnsNull();

        var result = await sutProvider.Sut.GetPoliciesAsync(code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesAsync_OrganizationDisabled_ReturnsNotFoundError(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkPoliciesQuery> sutProvider)
    {
        organization.Enabled = false;
        inviteLink.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(code).Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id).Returns(organization);

        var result = await sutProvider.Sut.GetPoliciesAsync(code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesAsync_UseInviteLinksFalse_ReturnsNotAvailableError(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkPoliciesQuery> sutProvider)
    {
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        organization.UseInviteLinks = false;

        var result = await sutProvider.Sut.GetPoliciesAsync(code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesAsync_UsePoliciesFalse_ReturnsNotFoundError(
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkPoliciesQuery> sutProvider)
    {
        inviteLink.OrganizationId = organization.Id;

        SetupMocks(sutProvider, code, inviteLink, organization);
        organization.UsePolicies = false;

        var result = await sutProvider.Sut.GetPoliciesAsync(code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);

        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(default);
    }

    private static void SetupMocks(
        SutProvider<GetOrganizationInviteLinkPoliciesQuery> sutProvider,
        Guid code,
        OrganizationInviteLink inviteLink,
        Organization organization)
    {
        organization.Enabled = true;
        organization.UseInviteLinks = true;
        organization.UsePolicies = true;
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByCodeAsync(code)
            .Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
    }
}
