using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationSponsorshipFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise;

[SutProviderCustomize]
public class CreateSponsorshipCommandTests : FamiliesForEnterpriseTestsBase
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
    public async Task CreateSponsorship_OfferedToNotFound_ThrowsBadRequest(OrganizationUser orgUser, SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(orgUser.UserId.Value).ReturnsNull();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateSponsorshipAsync(null, orgUser, PlanSponsorshipType.FamiliesForEnterprise, default, default));

        Assert.Contains("Cannot offer a Families Organization Sponsorship to yourself. Choose a different email.", exception.Message);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
    }

    [Theory, BitAutoData]
    public async Task CreateSponsorship_OfferedToSelf_ThrowsBadRequest(OrganizationUser orgUser, string sponsoredEmail, User user, SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        user.Email = sponsoredEmail;
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(orgUser.UserId.Value).Returns(user);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateSponsorshipAsync(null, orgUser, PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, default));

        Assert.Contains("Cannot offer a Families Organization Sponsorship to yourself. Choose a different email.", exception.Message);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
    }

    [Theory, BitMemberAutoData(nameof(NonEnterprisePlanTypes))]
    public async Task CreateSponsorship_BadSponsoringOrgPlan_ThrowsBadRequest(PlanType sponsoringOrgPlan,
        Organization org, OrganizationUser orgUser, User user, SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        org.PlanType = sponsoringOrgPlan;
        orgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(orgUser.UserId.Value).Returns(user);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateSponsorshipAsync(org, orgUser, PlanSponsorshipType.FamiliesForEnterprise, default, default));

        Assert.Contains("Specified Organization cannot sponsor other organizations.", exception.Message);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
    }

    [Theory]
    [BitMemberAutoData(nameof(NonConfirmedOrganizationUsersStatuses))]
    public async Task CreateSponsorship_BadSponsoringUserStatus_ThrowsBadRequest(
        OrganizationUserStatusType statusType, Organization org, OrganizationUser orgUser, User user,
        SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.Status = statusType;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(orgUser.UserId.Value).Returns(user);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateSponsorshipAsync(org, orgUser, PlanSponsorshipType.FamiliesForEnterprise, default, default));

        Assert.Contains("Only confirmed users can sponsor other organizations.", exception.Message);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
    }

    [Theory]
    [OrganizationSponsorshipCustomize]
    [BitAutoData]
    public async Task CreateSponsorship_AlreadySponsoring_Throws(Organization org,
        OrganizationUser orgUser, User user, OrganizationSponsorship sponsorship,
        SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(orgUser.UserId.Value).Returns(user);
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetBySponsoringOrganizationUserIdAsync(orgUser.Id).Returns(sponsorship);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateSponsorshipAsync(org, orgUser, sponsorship.PlanSponsorshipType.Value, default, default));

        Assert.Contains("Can only sponsor one organization per Organization User.", exception.Message);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateSponsorship_CreatesSponsorship(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser, User user,
        string sponsoredEmail, string friendlyName, Guid sponsorshipId, SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
        sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(sponsoringOrgUser.UserId.Value).Returns(user);
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>().WhenForAnyArgs(x => x.UpsertAsync(default)).Do(callInfo =>
        {
            var sponsorship = callInfo.Arg<OrganizationSponsorship>();
            sponsorship.Id = sponsorshipId;
        });


        await sutProvider.Sut.CreateSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
            PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName);

        var expectedSponsorship = new OrganizationSponsorship
        {
            Id = sponsorshipId,
            SponsoringOrganizationId = sponsoringOrg.Id,
            SponsoringOrganizationUserId = sponsoringOrgUser.Id,
            FriendlyName = friendlyName,
            OfferedToEmail = sponsoredEmail,
            PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
        };

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
            .UpsertAsync(Arg.Is<OrganizationSponsorship>(s => SponsorshipValidator(s, expectedSponsorship)));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateSponsorship_CreateSponsorshipThrows_RevertsDatabase(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser, User user,
        string sponsoredEmail, string friendlyName, SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
        sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

        var expectedException = new Exception();
        OrganizationSponsorship createdSponsorship = null;
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(sponsoringOrgUser.UserId.Value).Returns(user);
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>().UpsertAsync(default).ThrowsForAnyArgs(callInfo =>
        {
            createdSponsorship = callInfo.ArgAt<OrganizationSponsorship>(0);
            createdSponsorship.Id = Guid.NewGuid();
            return expectedException;
        });

        var actualException = await Assert.ThrowsAsync<Exception>(() =>
            sutProvider.Sut.CreateSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName));
        Assert.Same(expectedException, actualException);

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
            .DeleteAsync(createdSponsorship);
    }
}
