using Bit.Api.Billing.Controllers;
using Bit.Api.Models.Request.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Billing.Enums;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Core.Test.Billing.Mocks;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.Billing.Controllers;

[ControllerCustomize(typeof(OrganizationSponsorshipsController))]
[SutProviderCustomize]
public class OrganizationSponsorshipsControllerTests
{
    public static IEnumerable<object[]> EnterprisePlanTypes =>
        Enum.GetValues<PlanType>().Where(p => MockPlans.Get(p).ProductTier == ProductTierType.Enterprise).Select(p => new object[] { p });
    public static IEnumerable<object[]> NonEnterprisePlanTypes =>
        Enum.GetValues<PlanType>().Where(p => MockPlans.Get(p).ProductTier != ProductTierType.Enterprise).Select(p => new object[] { p });
    public static IEnumerable<object[]> NonFamiliesPlanTypes =>
        Enum.GetValues<PlanType>().Where(p => MockPlans.Get(p).ProductTier != ProductTierType.Families).Select(p => new object[] { p });

    public static IEnumerable<object[]> NonConfirmedOrganizationUsersStatuses =>
        Enum.GetValues<OrganizationUserStatusType>()
            .Where(s => s != OrganizationUserStatusType.Confirmed)
            .Select(s => new object[] { s });


    [Theory]
    [BitAutoData]
    public async Task RedeemSponsorship_BadToken_ThrowsBadRequest(string sponsorshipToken, User user,
        OrganizationSponsorshipRedeemRequestModel model, SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id)
            .Returns(user);
        sutProvider.GetDependency<IValidateRedemptionTokenCommand>().ValidateRedemptionTokenAsync(sponsorshipToken,
            user.Email).Returns((false, null));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

        Assert.Contains("Failed to parse sponsorship token.", exception.Message);
        await sutProvider.GetDependency<ISetUpSponsorshipCommand>()
            .DidNotReceiveWithAnyArgs()
            .SetUpSponsorshipAsync(default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task RedeemSponsorship_NotSponsoredOrgOwner_ThrowsBadRequest(string sponsorshipToken, User user,
        OrganizationSponsorship sponsorship, OrganizationSponsorshipRedeemRequestModel model,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id)
            .Returns(user);
        sutProvider.GetDependency<IValidateRedemptionTokenCommand>().ValidateRedemptionTokenAsync(sponsorshipToken,
            user.Email).Returns((true, sponsorship));
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

        Assert.Contains("Can only redeem sponsorship for an organization you own.", exception.Message);
        await sutProvider.GetDependency<ISetUpSponsorshipCommand>()
            .DidNotReceiveWithAnyArgs()
            .SetUpSponsorshipAsync(default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task RedeemSponsorship_NotSponsoredOrgOwner_Success(string sponsorshipToken, User user,
        OrganizationSponsorship sponsorship, Organization sponsoringOrganization,
        OrganizationSponsorshipRedeemRequestModel model,
        [Policy(PolicyType.FreeFamiliesSponsorshipPolicy, false)] PolicyStatus policy,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id)
            .Returns(user);
        sutProvider.GetDependency<IValidateRedemptionTokenCommand>().ValidateRedemptionTokenAsync(sponsorshipToken,
            user.Email).Returns((true, sponsorship));
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(model.SponsoredOrganizationId).Returns(sponsoringOrganization);
        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(Arg.Any<Guid>(), PolicyType.FreeFamiliesSponsorshipPolicy)
            .Returns(policy);

        await sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model);

        await sutProvider.GetDependency<ISetUpSponsorshipCommand>().Received(1)
            .SetUpSponsorshipAsync(sponsorship, sponsoringOrganization);
    }

    [Theory]
    [BitAutoData]
    public async Task PreValidateSponsorshipToken_ValidatesToken_Success(string sponsorshipToken, User user,
        OrganizationSponsorship sponsorship, Organization sponsoringOrganization,
        [Policy(PolicyType.FreeFamiliesSponsorshipPolicy, false)] PolicyStatus policy,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id)
            .Returns(user);
        sutProvider.GetDependency<IValidateRedemptionTokenCommand>()
            .ValidateRedemptionTokenAsync(sponsorshipToken, user.Email).Returns((true, sponsorship));
        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(Arg.Any<Guid>(), PolicyType.FreeFamiliesSponsorshipPolicy)
            .Returns(policy);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(sponsorship.SponsoringOrganizationId!.Value).Returns(sponsoringOrganization);

        var response = await sutProvider.Sut.PreValidateSponsorshipToken(sponsorshipToken);

        await sutProvider.GetDependency<IValidateRedemptionTokenCommand>().Received(1)
            .ValidateRedemptionTokenAsync(sponsorshipToken, user.Email);
        Assert.True(response.IsTokenValid);
        Assert.Equal(sponsoringOrganization.DisplayName(), response.SponsoringOrganizationName);
    }

    [Theory]
    [BitAutoData]
    public async Task PreValidateSponsorshipToken_InvalidToken_DoesNotReturnSponsoringOrganizationName(
        string sponsorshipToken, User user, OrganizationSponsorship sponsorship,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id)
            .Returns(user);
        sutProvider.GetDependency<IValidateRedemptionTokenCommand>()
            .ValidateRedemptionTokenAsync(sponsorshipToken, user.Email).Returns((false, sponsorship));

        var response = await sutProvider.Sut.PreValidateSponsorshipToken(sponsorshipToken);

        Assert.False(response.IsTokenValid);
        Assert.Null(response.SponsoringOrganizationName);
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task RevokeSponsorship_WrongSponsoringUser_ThrowsBadRequest(OrganizationUser sponsoringOrgUser,
        Guid currentUserId, SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(currentUserId);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(sponsoringOrgUser.Id)
            .Returns(sponsoringOrgUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RevokeSponsorship(sponsoringOrgUser.Id));

        Assert.Contains("Can only revoke a sponsorship you granted.", exception.Message);
        await sutProvider.GetDependency<IRemoveSponsorshipCommand>()
            .DidNotReceiveWithAnyArgs()
            .RemoveSponsorshipAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task RemoveSponsorship_WrongOrgUserType_ThrowsBadRequest(Organization sponsoredOrg,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(Arg.Any<Guid>()).Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RemoveSponsorship(sponsoredOrg.Id));

        Assert.Contains("Only the owner of an organization can remove sponsorship.", exception.Message);
        await sutProvider.GetDependency<IRemoveSponsorshipCommand>()
            .DidNotReceiveWithAnyArgs()
            .RemoveSponsorshipAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSponsoredOrganizations_OrganizationNotFound_ThrowsNotFound(
        Guid sponsoringOrgId,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrgId).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetSponsoredOrganizations(sponsoringOrgId));

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyBySponsoringOrganizationAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSponsoredOrganizations_NotOrganizationOwner_ThrowsNotFound(
        Organization sponsoringOrg,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(sponsoringOrg.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(sponsoringOrg.Id).Returns(false);

        // Create a CurrentContextOrganization with ManageUsers set to false
        var currentContextOrg = new CurrentContextOrganization
        {
            Id = sponsoringOrg.Id,
            Permissions = new Permissions { ManageUsers = false }
        };
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(new List<CurrentContextOrganization> { currentContextOrg });

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sutProvider.Sut.GetSponsoredOrganizations(sponsoringOrg.Id));

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyBySponsoringOrganizationAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSponsoredOrganizations_Success_ReturnsSponsorships(
        Organization sponsoringOrg,
        List<OrganizationSponsorship> sponsorships,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(sponsoringOrg.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(sponsoringOrg.Id).Returns(false);

        // Create a CurrentContextOrganization from the sponsoringOrg
        var currentContextOrg = new CurrentContextOrganization
        {
            Id = sponsoringOrg.Id,
            Permissions = new Permissions { ManageUsers = true }
        };
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(new List<CurrentContextOrganization> { currentContextOrg });

        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(sponsoringOrg.Id).Returns(sponsorships);

        // Set IsAdminInitiated to true for all test sponsorships
        foreach (var sponsorship in sponsorships)
        {
            sponsorship.IsAdminInitiated = true;
        }

        // Act
        var result = await sutProvider.Sut.GetSponsoredOrganizations(sponsoringOrg.Id);

        // Assert
        Assert.Equal(sponsorships.Count, result.Data.Count());
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
            .GetManyBySponsoringOrganizationAsync(sponsoringOrg.Id);
    }

    /// <summary>
    /// A member-initiated sponsorship belongs to the member's own personal Families organization.
    /// It is hidden from GET {orgId}/sponsored, so the admin revoke route must not act on it
    /// either, and must be indistinguishable from a friendly name that does not exist.
    /// </summary>
    [Theory]
    [BitAutoData]
    public async Task AdminInitiatedRevokeSponsorshipAsync_MemberInitiatedSponsorship_ThrowsBadRequest(
        Guid sponsoringOrgId,
        OrganizationSponsorship sponsorship,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        // Arrange
        sponsorship.IsAdminInitiated = false;
        sponsorship.FriendlyName = "personal-family@example.com";
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(sponsoringOrgId)
            .Returns(new List<OrganizationSponsorship> { sponsorship });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdminInitiatedRevokeSponsorshipAsync(sponsoringOrgId, sponsorship.FriendlyName));

        Assert.Contains("could not be found under the given sponsoring organization", exception.Message);
        await sutProvider.GetDependency<IRevokeSponsorshipCommand>()
            .DidNotReceiveWithAnyArgs()
            .RevokeSponsorshipAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task AdminInitiatedRevokeSponsorshipAsync_AdminInitiatedSponsorship_Revokes(
        Guid sponsoringOrgId,
        OrganizationSponsorship sponsorship,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        // Arrange
        sponsorship.IsAdminInitiated = true;
        sponsorship.FriendlyName = "employee@example.com";
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(sponsoringOrgId)
            .Returns(new List<OrganizationSponsorship> { sponsorship });

        // Act
        await sutProvider.Sut.AdminInitiatedRevokeSponsorshipAsync(sponsoringOrgId, sponsorship.FriendlyName);

        // Assert
        await sutProvider.GetDependency<IRevokeSponsorshipCommand>().Received(1)
            .RevokeSponsorshipAsync(sponsorship);
    }

    /// <summary>
    /// The resend route shares the same manageUsers gate and the same friendly-name lookup, so it
    /// must not re-offer a colleague's member-initiated sponsorship either.
    /// </summary>
    [Theory]
    [BitAutoData]
    public async Task ResendSponsorshipOffer_AnotherMembersMemberInitiatedSponsorship_DoesNotSend(
        Guid sponsoringOrgId,
        OrganizationUser callingOrgUser,
        OrganizationSponsorship sponsorship,
        [Policy(PolicyType.FreeFamiliesSponsorshipPolicy, false)] PolicyStatus policy,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        // Arrange
        sponsorship.IsAdminInitiated = false;
        sponsorship.FriendlyName = "personal-family@example.com";
        sponsorship.SponsoringOrganizationUserId = Guid.NewGuid();

        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(sponsoringOrgId, PolicyType.FreeFamiliesSponsorshipPolicy).Returns(policy);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(sponsoringOrgId, Arg.Any<Guid>()).Returns(callingOrgUser);
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(sponsoringOrgId)
            .Returns(new List<OrganizationSponsorship> { sponsorship });

        // Act
        await sutProvider.Sut.ResendSponsorshipOffer(sponsoringOrgId, sponsorship.FriendlyName);

        // Assert
        await sutProvider.GetDependency<ISendSponsorshipOfferCommand>()
            .DidNotReceiveWithAnyArgs()
            .SendSponsorshipOfferAsync(default, default, default);
    }

    /// <summary>
    /// The personal Settings > Sponsored families page posts to this same admin route, so an
    /// Owner/Admin/manageUsers member must still be able to resend their OWN member-initiated offer.
    /// </summary>
    [Theory]
    [BitAutoData]
    public async Task ResendSponsorshipOffer_OwnMemberInitiatedSponsorship_Sends(
        Guid sponsoringOrgId,
        Organization sponsoringOrg,
        OrganizationUser callingOrgUser,
        OrganizationSponsorship sponsorship,
        [Policy(PolicyType.FreeFamiliesSponsorshipPolicy, false)] PolicyStatus policy,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        // Arrange
        sponsorship.IsAdminInitiated = false;
        sponsorship.FriendlyName = "my-family";
        sponsorship.SponsoringOrganizationUserId = callingOrgUser.Id;

        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(sponsoringOrgId, PolicyType.FreeFamiliesSponsorshipPolicy).Returns(policy);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(sponsoringOrgId, Arg.Any<Guid>()).Returns(callingOrgUser);
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(sponsoringOrgId)
            .Returns(new List<OrganizationSponsorship> { sponsorship });
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(sponsoringOrgId).Returns(sponsoringOrg);

        // Act
        await sutProvider.Sut.ResendSponsorshipOffer(sponsoringOrgId, sponsorship.FriendlyName);

        // Assert
        await sutProvider.GetDependency<ISendSponsorshipOfferCommand>().Received(1)
            .SendSponsorshipOfferAsync(sponsoringOrg, callingOrgUser, sponsorship);
    }

    /// <summary>
    /// Admin-initiated sponsorships are the ones this route exists for, so a manageUsers holder
    /// must still be able to resend one granted by a different member.
    /// </summary>
    [Theory]
    [BitAutoData]
    public async Task ResendSponsorshipOffer_AnotherMembersAdminInitiatedSponsorship_Sends(
        Guid sponsoringOrgId,
        Organization sponsoringOrg,
        OrganizationUser callingOrgUser,
        OrganizationSponsorship sponsorship,
        [Policy(PolicyType.FreeFamiliesSponsorshipPolicy, false)] PolicyStatus policy,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        // Arrange
        sponsorship.IsAdminInitiated = true;
        sponsorship.FriendlyName = "employee@example.com";
        sponsorship.SponsoringOrganizationUserId = Guid.NewGuid();

        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(sponsoringOrgId, PolicyType.FreeFamiliesSponsorshipPolicy).Returns(policy);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(sponsoringOrgId, Arg.Any<Guid>()).Returns(callingOrgUser);
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(sponsoringOrgId)
            .Returns(new List<OrganizationSponsorship> { sponsorship });
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(sponsoringOrgId).Returns(sponsoringOrg);

        // Act
        await sutProvider.Sut.ResendSponsorshipOffer(sponsoringOrgId, sponsorship.FriendlyName);

        // Assert
        await sutProvider.GetDependency<ISendSponsorshipOfferCommand>().Received(1)
            .SendSponsorshipOfferAsync(sponsoringOrg, callingOrgUser, sponsorship);
    }
}
