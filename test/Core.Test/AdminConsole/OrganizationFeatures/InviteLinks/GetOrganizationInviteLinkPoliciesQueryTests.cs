using Bit.Core.AdminConsole.Entities;
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
    public async Task GetPoliciesAsync_WithValidInput_ReturnsOnlyEnabledPolicies(
        OrganizationInviteLink inviteLink,
        Organization organization,
        List<Policy> policies,
        SutProvider<GetOrganizationInviteLinkPoliciesQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.UsePolicies = true;
        inviteLink.Code = code.ToString();

        policies[0].Enabled = true;
        policies[1].Enabled = false;
        policies[2].Enabled = true;

        SetupMocks(sutProvider, inviteLink, organization);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(inviteLink.OrganizationId)
            .Returns(policies);

        var result = await sutProvider.Sut.GetPoliciesAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsSuccess);
        var returned = result.AsSuccess.ToList();
        Assert.Equal(2, returned.Count);
        Assert.All(returned, p => Assert.True(p.Enabled));
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesAsync_InviteLinkNotFound_ReturnsNotFoundError(
        Guid organizationId,
        Guid code,
        SutProvider<GetOrganizationInviteLinkPoliciesQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId).ReturnsNull();

        var result = await sutProvider.Sut.GetPoliciesAsync(organizationId, code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesAsync_CodeMismatch_ReturnsNotFoundError(
        OrganizationInviteLink inviteLink,
        SutProvider<GetOrganizationInviteLinkPoliciesQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(inviteLink);

        var result = await sutProvider.Sut.GetPoliciesAsync(inviteLink.OrganizationId, Guid.NewGuid());

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesAsync_OrganizationNotFound_ReturnsNotFoundError(
        OrganizationInviteLink inviteLink,
        SutProvider<GetOrganizationInviteLinkPoliciesQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        inviteLink.Code = code.ToString();

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(inviteLink.OrganizationId).ReturnsNull();

        var result = await sutProvider.Sut.GetPoliciesAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesAsync_OrganizationDisabled_ReturnsNotFoundError(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkPoliciesQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Enabled = false;
        inviteLink.Code = code.ToString();

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(inviteLink.OrganizationId).Returns(organization);

        var result = await sutProvider.Sut.GetPoliciesAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesAsync_UseInviteLinksFalse_ReturnsNotAvailableError(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkPoliciesQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        organization.UseInviteLinks = false;

        var result = await sutProvider.Sut.GetPoliciesAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesAsync_UsePoliciesFalse_ReturnsNotFoundError(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<GetOrganizationInviteLinkPoliciesQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        inviteLink.Code = code.ToString();

        SetupMocks(sutProvider, inviteLink, organization);
        organization.UsePolicies = false;

        var result = await sutProvider.Sut.GetPoliciesAsync(inviteLink.OrganizationId, code);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);

        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(default);
    }

    private static void SetupMocks(
        SutProvider<GetOrganizationInviteLinkPoliciesQuery> sutProvider,
        OrganizationInviteLink inviteLink,
        Organization organization)
    {
        organization.Enabled = true;
        organization.UseInviteLinks = true;
        organization.UsePolicies = true;
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId)
            .Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
    }
}
