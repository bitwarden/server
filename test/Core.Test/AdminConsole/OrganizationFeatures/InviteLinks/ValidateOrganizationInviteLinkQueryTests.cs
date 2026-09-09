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
public class ValidateOrganizationInviteLinkQueryTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_WithValidLinkAndAllowedDomainEmail_Success(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<ValidateOrganizationInviteLinkQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Enabled = true;
        organization.UseInviteLinks = true;
        inviteLink.Code = code.ToString();
        inviteLink.SetAllowedDomains(new[] { "example.com" });
        var email = "user@example.com";

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id).Returns(organization);

        var result = await sutProvider.Sut.ValidateAsync(inviteLink.OrganizationId, code, email);

        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .Received(1).GetByOrganizationIdAsync(inviteLink.OrganizationId);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1).GetByIdAsync(organization.Id);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_InviteLinkNotFound_ReturnsInviteLinkNotFound(
        Guid organizationId,
        Guid code,
        string email,
        SutProvider<ValidateOrganizationInviteLinkQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(organizationId).ReturnsNull();

        var result = await sutProvider.Sut.ValidateAsync(organizationId, code, email);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);

        // Short-circuit: org repo must not be consulted when the link doesn't exist.
        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_CodeMismatch_ReturnsInviteLinkNotFound(
        OrganizationInviteLink inviteLink,
        string email,
        SutProvider<ValidateOrganizationInviteLinkQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(inviteLink);

        var result = await sutProvider.Sut.ValidateAsync(inviteLink.OrganizationId, Guid.NewGuid(), email);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);

        // Short-circuit: org repo must not be consulted when the code doesn't match.
        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceiveWithAnyArgs().GetByIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_OrganizationNotFound_ReturnsInviteLinkNotFound(
        OrganizationInviteLink inviteLink,
        string email,
        SutProvider<ValidateOrganizationInviteLinkQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        inviteLink.Code = code.ToString();

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(inviteLink.OrganizationId).ReturnsNull();

        var result = await sutProvider.Sut.ValidateAsync(inviteLink.OrganizationId, code, email);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_OrganizationDisabled_ReturnsInviteLinkNotFound(
        OrganizationInviteLink inviteLink,
        Organization organization,
        string email,
        SutProvider<ValidateOrganizationInviteLinkQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Enabled = false;
        // Explicit so this test pins "disabled trumps UseInviteLinks" independent of autofixture defaults.
        organization.UseInviteLinks = true;
        inviteLink.Code = code.ToString();

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id).Returns(organization);

        var result = await sutProvider.Sut.ValidateAsync(inviteLink.OrganizationId, code, email);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_UseInviteLinksFalse_ReturnsInviteLinkNotAvailable(
        OrganizationInviteLink inviteLink,
        Organization organization,
        string email,
        SutProvider<ValidateOrganizationInviteLinkQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Enabled = true;
        organization.UseInviteLinks = false;
        inviteLink.Code = code.ToString();

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id).Returns(organization);

        var result = await sutProvider.Sut.ValidateAsync(inviteLink.OrganizationId, code, email);

        Assert.True(result.IsError);
        Assert.IsType<InviteLinkNotAvailable>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EmailDomainNotInAllowedDomains_ReturnsEmailDomainNotAllowed(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<ValidateOrganizationInviteLinkQuery> sutProvider)
    {
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Enabled = true;
        organization.UseInviteLinks = true;
        inviteLink.Code = code.ToString();
        inviteLink.SetAllowedDomains(new[] { "partner.com" });
        var email = "user@example.com";

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id).Returns(organization);

        var result = await sutProvider.Sut.ValidateAsync(inviteLink.OrganizationId, code, email);

        Assert.True(result.IsError);
        Assert.IsType<EmailDomainNotAllowed>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EmptyAllowedDomains_ReturnsEmailDomainNotAllowed(
        OrganizationInviteLink inviteLink,
        Organization organization,
        SutProvider<ValidateOrganizationInviteLinkQuery> sutProvider)
    {
        // Empty AllowedDomains means the link admits no email domain (per InviteLinkDomainValidator).
        var code = Guid.NewGuid();
        organization.Id = inviteLink.OrganizationId;
        organization.Enabled = true;
        organization.UseInviteLinks = true;
        inviteLink.Code = code.ToString();
        inviteLink.SetAllowedDomains(Array.Empty<string>());
        var email = "user@example.com";

        sutProvider.GetDependency<IOrganizationInviteLinkRepository>()
            .GetByOrganizationIdAsync(inviteLink.OrganizationId).Returns(inviteLink);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id).Returns(organization);

        var result = await sutProvider.Sut.ValidateAsync(inviteLink.OrganizationId, code, email);

        Assert.True(result.IsError);
        Assert.IsType<EmailDomainNotAllowed>(result.AsError);
    }
}
