using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SponsorshipCreation;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationSponsorshipFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SponsorshipCreation;

[SutProviderCustomize]
public class CreateSponsorshipHandlerTests : FamiliesForEnterpriseTestsBase
{
    private bool SponsorshipValidator(OrganizationSponsorship sponsorship, OrganizationSponsorship expectedSponsorship)
    {
        try
        {
            AssertHelper.AssertPropertyEqual(sponsorship, expectedSponsorship, nameof(OrganizationSponsorship.Id));
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Theory, BitAutoData]
    public async Task CreateSponsorship_OfferedToNotFound_ThrowsBadRequest(OrganizationUser orgUser, SutProvider<CreateSponsorshipHandler> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(orgUser.UserId!.Value).ReturnsNull();
        var request = new CreateSponsorshipRequest(null, orgUser, PlanSponsorshipType.FamiliesForEnterprise, null, null, null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.HandleAsync(request));

        Assert.Contains("Cannot offer a Families Organization Sponsorship to yourself. Choose a different email.", exception.Message);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(null!);
    }

    [Theory, BitAutoData]
    public async Task CreateSponsorship_OfferedToSelf_ThrowsBadRequest(OrganizationUser orgUser, string sponsoredEmail, User user, SutProvider<CreateSponsorshipHandler> sutProvider)
    {
        user.Email = sponsoredEmail;
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(orgUser.UserId!.Value).Returns(user);
        var request = new CreateSponsorshipRequest(null, orgUser, PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, null, null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.HandleAsync(request));

        Assert.Contains("Cannot offer a Families Organization Sponsorship to yourself. Choose a different email.", exception.Message);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(null!);
    }

    [Theory, BitMemberAutoData(nameof(NonEnterprisePlanTypes))]
    public async Task CreateSponsorship_BadSponsoringOrgPlan_ThrowsBadRequest(PlanType sponsoringOrgPlan,
        Organization org, OrganizationUser orgUser, User user, SutProvider<CreateSponsorshipHandler> sutProvider)
    {
        org.PlanType = sponsoringOrgPlan;
        orgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(orgUser.UserId!.Value).Returns(user);

        var request = new CreateSponsorshipRequest(org, orgUser, PlanSponsorshipType.FamiliesForEnterprise, null, null, null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.HandleAsync(request));

        Assert.Contains("Specified Organization cannot sponsor other organizations.", exception.Message);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(null!);
    }

    [Theory]
    [BitMemberAutoData(nameof(NonConfirmedOrganizationUsersStatuses))]
    public async Task CreateSponsorship_BadSponsoringUserStatus_ThrowsBadRequest(
        OrganizationUserStatusType statusType, Organization org, OrganizationUser orgUser, User user,
        SutProvider<CreateSponsorshipHandler> sutProvider)
    {
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.Status = statusType;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(orgUser.UserId!.Value).Returns(user);

        var request = new CreateSponsorshipRequest(org, orgUser, PlanSponsorshipType.FamiliesForEnterprise, null, null, null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.HandleAsync(request));

        Assert.Contains("Only confirmed users can sponsor other organizations.", exception.Message);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(null!);
    }

    [Theory]
    [OrganizationSponsorshipCustomize]
    [BitAutoData]
    public async Task CreateSponsorship_AlreadySponsoring_Throws(Organization org,
        OrganizationUser orgUser, User user, OrganizationSponsorship sponsorship,
        SutProvider<CreateSponsorshipHandler> sutProvider)
    {
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(orgUser.UserId!.Value).Returns(user);
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetBySponsoringOrganizationUserIdAsync(orgUser.Id).Returns(sponsorship);

        var request = new CreateSponsorshipRequest(org, orgUser, sponsorship.PlanSponsorshipType!.Value, null, null, null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.HandleAsync(request));

        Assert.Contains("Can only sponsor one organization per Organization User.", exception.Message);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(null!);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateSponsorship_ReturnsExpectedSponsorship(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser, User user,
        string sponsoredEmail, string friendlyName, SutProvider<CreateSponsorshipHandler> sutProvider)
    {
        sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
        sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(sponsoringOrgUser.UserId!.Value).Returns(user);


        var request = new CreateSponsorshipRequest(sponsoringOrg, sponsoringOrgUser,
            PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, null);

        var actual = await sutProvider.Sut.HandleAsync(request);

        var expectedSponsorship = new OrganizationSponsorship
        {
            SponsoringOrganizationId = sponsoringOrg.Id,
            SponsoringOrganizationUserId = sponsoringOrgUser.Id,
            FriendlyName = friendlyName,
            OfferedToEmail = sponsoredEmail,
            PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
            IsAdminInitiated = false,
            Notes = null
        };

        Assert.Equal(expectedSponsorship.SponsoringOrganizationId, actual.SponsoringOrganizationId);
        Assert.Equal(expectedSponsorship.SponsoringOrganizationUserId, actual.SponsoringOrganizationUserId);
        Assert.Equal(expectedSponsorship.FriendlyName, actual.FriendlyName);
        Assert.Equal(expectedSponsorship.OfferedToEmail, actual.OfferedToEmail);
        Assert.Equal(expectedSponsorship.PlanSponsorshipType, actual.PlanSponsorshipType);
        Assert.Equal(expectedSponsorship.IsAdminInitiated, actual.IsAdminInitiated);
        Assert.Equal(expectedSponsorship.Notes, actual.Notes);
    }
}
