using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
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
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(orgUser.UserId!.Value).ReturnsNull();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateSponsorshipAsync(null, orgUser, PlanSponsorshipType.FamiliesForEnterprise, default, default, false, null));

        Assert.Contains("Cannot offer a Families Organization Sponsorship to yourself. Choose a different email.", exception.Message);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(null!);
    }

    [Theory, BitAutoData]
    public async Task CreateSponsorship_OfferedToSelf_ThrowsBadRequest(OrganizationUser orgUser, string sponsoredEmail, User user, SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        user.Email = sponsoredEmail;
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(orgUser.UserId!.Value).Returns(user);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateSponsorshipAsync(null, orgUser, PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, default, false, null));

        Assert.Contains("Cannot offer a Families Organization Sponsorship to yourself. Choose a different email.", exception.Message);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(null!);
    }

    [Theory, BitMemberAutoData(nameof(NonEnterprisePlanTypes))]
    public async Task CreateSponsorship_BadSponsoringOrgPlan_ThrowsBadRequest(PlanType sponsoringOrgPlan,
        Organization org, OrganizationUser orgUser, User user, SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        org.PlanType = sponsoringOrgPlan;
        orgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(orgUser.UserId!.Value).Returns(user);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateSponsorshipAsync(org, orgUser, PlanSponsorshipType.FamiliesForEnterprise, default, default, false, null));

        Assert.Contains("Specified Organization cannot sponsor other organizations.", exception.Message);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(null!);
    }

    [Theory]
    [BitMemberAutoData(nameof(NonConfirmedOrganizationUsersStatuses))]
    public async Task CreateSponsorship_BadSponsoringUserStatus_ThrowsBadRequest(
        OrganizationUserStatusType statusType, Organization org, OrganizationUser orgUser, User user,
        SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.Status = statusType;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(orgUser.UserId!.Value).Returns(user);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateSponsorshipAsync(org, orgUser, PlanSponsorshipType.FamiliesForEnterprise, default, default, false, null));

        Assert.Contains("Only confirmed users can sponsor other organizations.", exception.Message);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(null!);
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

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(orgUser.UserId!.Value).Returns(user);
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetBySponsoringOrganizationUserIdAsync(orgUser.Id).Returns(sponsorship);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId.Value);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateSponsorshipAsync(org, orgUser, sponsorship.PlanSponsorshipType!.Value, null, null, false, null));

        Assert.Contains("Can only sponsor one organization per Organization User.", exception.Message);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(null!);
    }

    public static readonly OrganizationUserStatusType[] UnconfirmedOrganizationUsersStatuses = Enum
        .GetValues<OrganizationUserStatusType>()
        .Where(x => x != OrganizationUserStatusType.Confirmed)
        .ToArray();

    [Theory]
    [BitMemberAutoData(nameof(UnconfirmedOrganizationUsersStatuses))]
    public async Task CreateSponsorship_ThrowsBadRequestException_WhenMemberDoesNotHaveConfirmedStatusInOrganization(
        OrganizationUserStatusType status, Organization sponsoringOrg, OrganizationUser sponsoringOrgUser, User user,
        string sponsoredEmail, string friendlyName, Guid sponsorshipId,
        SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
        sponsoringOrgUser.Status = status;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(sponsoringOrgUser.UserId!.Value).Returns(user);
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>().WhenForAnyArgs(x => x.UpsertAsync(null!)).Do(callInfo =>
        {
            var sponsorship = callInfo.Arg<OrganizationSponsorship>();
            sponsorship.Id = sponsorshipId;
        });
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(sponsoringOrgUser.UserId.Value);


        var actual = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.CreateSponsorshipAsync(sponsoringOrg, sponsoringOrgUser, PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, false, null));

        Assert.Equal("Only confirmed users can sponsor other organizations.", actual.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateSponsorship_CreatesSponsorship(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser, User user,
        string sponsoredEmail, string friendlyName, Guid sponsorshipId, SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
        sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(sponsoringOrgUser.UserId!.Value).Returns(user);
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>().WhenForAnyArgs(x => x.UpsertAsync(null!)).Do(callInfo =>
        {
            var sponsorship = callInfo.Arg<OrganizationSponsorship>();
            sponsorship.Id = sponsorshipId;
        });
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(sponsoringOrgUser.UserId.Value);

        // Setup for checking available seats
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(sponsoringOrg.Id)
            .Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 0
            });


        await sutProvider.Sut.CreateSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
            PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, false, null);

        var expectedSponsorship = new OrganizationSponsorship
        {
            Id = sponsorshipId,
            SponsoringOrganizationId = sponsoringOrg.Id,
            SponsoringOrganizationUserId = sponsoringOrgUser.Id,
            FriendlyName = friendlyName,
            OfferedToEmail = sponsoredEmail,
            PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
            IsAdminInitiated = false,
            Notes = null
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
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(sponsoringOrgUser.UserId!.Value).Returns(user);
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>().UpsertAsync(null!).ThrowsForAnyArgs(callInfo =>
        {
            createdSponsorship = callInfo.ArgAt<OrganizationSponsorship>(0);
            createdSponsorship.Id = Guid.NewGuid();
            return expectedException;
        });
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(sponsoringOrgUser.UserId.Value);

        var actualException = await Assert.ThrowsAsync<Exception>(() =>
            sutProvider.Sut.CreateSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, false, null));
        Assert.Same(expectedException, actualException);

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
            .DeleteAsync(createdSponsorship);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateSponsorship_MissingManageUsersPermission_ThrowsUnauthorizedException(
        Organization sponsoringOrg, OrganizationUser sponsoringOrgUser, User user, string sponsoredEmail,
        string friendlyName, Guid sponsorshipId, Guid currentUserId, SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
        sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(sponsoringOrgUser.UserId!.Value).Returns(user);
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>().WhenForAnyArgs(x => x.UpsertAsync(null!)).Do(callInfo =>
        {
            var sponsorship = callInfo.Arg<OrganizationSponsorship>();
            sponsorship.Id = sponsorshipId;
        });
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(currentUserId);
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns([
            new()
            {
                Id = sponsoringOrg.Id,
                Permissions = new Permissions(),
                Type = OrganizationUserType.Custom
            }
        ]);


        var actual = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await sutProvider.Sut.CreateSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, true, null));

        Assert.Equal("You do not have permissions to send sponsorships on behalf of the organization", actual.Message);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task CreateSponsorship_InvalidUserType_ThrowsUnauthorizedException(
        OrganizationUserType organizationUserType,
        Organization sponsoringOrg, OrganizationUser sponsoringOrgUser, User user, string sponsoredEmail,
        string friendlyName, Guid sponsorshipId, Guid currentUserId, SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
        sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(sponsoringOrgUser.UserId!.Value).Returns(user);
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>().WhenForAnyArgs(x => x.UpsertAsync(null!)).Do(callInfo =>
        {
            var sponsorship = callInfo.Arg<OrganizationSponsorship>();
            sponsorship.Id = sponsorshipId;
        });
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(currentUserId);
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns([
            new()
            {
                Id = sponsoringOrg.Id,
                Permissions = new Permissions(),
                Type = organizationUserType
            }
        ]);

        var actual = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await sutProvider.Sut.CreateSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, true, null));

        Assert.Equal("You do not have permissions to send sponsorships on behalf of the organization", actual.Message);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CreateSponsorship_CreatesAdminInitiatedSponsorship(
        OrganizationUserType organizationUserType,
        Organization sponsoringOrg, OrganizationUser sponsoringOrgUser, User user, string sponsoredEmail,
        string friendlyName, Guid sponsorshipId, Guid currentUserId, string notes, SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
        sponsoringOrg.UseAdminSponsoredFamilies = true;
        sponsoringOrg.Seats = 10;
        sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(sponsoringOrgUser.UserId!.Value).Returns(user);
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>().WhenForAnyArgs(x => x.UpsertAsync(null!)).Do(callInfo =>
        {
            var sponsorship = callInfo.Arg<OrganizationSponsorship>();
            sponsorship.Id = sponsorshipId;
        });
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(currentUserId);
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns([
            new()
            {
                Id = sponsoringOrg.Id,
                Permissions = new Permissions { ManageUsers = true },
                Type = organizationUserType
            }
        ]);

        // Setup for checking available seats - organization has plenty of seats
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(sponsoringOrg.Id)
            .Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 5
            });

        var actual = await sutProvider.Sut.CreateSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
            PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, true, notes);


        var expectedSponsorship = new OrganizationSponsorship
        {
            Id = sponsorshipId,
            SponsoringOrganizationId = sponsoringOrg.Id,
            SponsoringOrganizationUserId = sponsoringOrgUser.Id,
            FriendlyName = friendlyName,
            OfferedToEmail = sponsoredEmail,
            PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
            IsAdminInitiated = true,
            Notes = notes
        };

        Assert.True(SponsorshipValidator(expectedSponsorship, actual));

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
            .CreateAsync(Arg.Is<OrganizationSponsorship>(s => SponsorshipValidator(s, expectedSponsorship)));

        // Verify we didn't need to add seats
        await sutProvider.GetDependency<IOrganizationService>().DidNotReceive()
            .AutoAddSeatsAsync(Arg.Any<Organization>(), Arg.Any<int>());
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CreateSponsorship_CreatesAdminInitiatedSponsorship_AutoscalesWhenNeeded(
        OrganizationUserType organizationUserType,
        Organization sponsoringOrg, OrganizationUser sponsoringOrgUser, User user, string sponsoredEmail,
        string friendlyName, Guid sponsorshipId, Guid currentUserId, string notes, SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
        sponsoringOrg.UseAdminSponsoredFamilies = true;
        sponsoringOrg.Seats = 10;
        sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(sponsoringOrgUser.UserId!.Value).Returns(user);
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>().WhenForAnyArgs(x => x.UpsertAsync(null!)).Do(callInfo =>
        {
            var sponsorship = callInfo.Arg<OrganizationSponsorship>();
            sponsorship.Id = sponsorshipId;
        });
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(currentUserId);
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns([
            new()
            {
                Id = sponsoringOrg.Id,
                Permissions = new Permissions { ManageUsers = true },
                Type = organizationUserType
            }
        ]);

        // Setup for checking available seats - organization has no available seats
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(sponsoringOrg.Id)
            .Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 10
            });

        // Setup for checking if can scale
        sutProvider.GetDependency<IOrganizationService>()
            .CanScaleAsync(sponsoringOrg, 1)
            .Returns((true, ""));

        var actual = await sutProvider.Sut.CreateSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
            PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, true, notes);


        var expectedSponsorship = new OrganizationSponsorship
        {
            Id = sponsorshipId,
            SponsoringOrganizationId = sponsoringOrg.Id,
            SponsoringOrganizationUserId = sponsoringOrgUser.Id,
            FriendlyName = friendlyName,
            OfferedToEmail = sponsoredEmail,
            PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
            IsAdminInitiated = true,
            Notes = notes
        };

        Assert.True(SponsorshipValidator(expectedSponsorship, actual));

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
            .CreateAsync(Arg.Is<OrganizationSponsorship>(s => SponsorshipValidator(s, expectedSponsorship)));

        // Verify we needed to add seats
        await sutProvider.GetDependency<IOrganizationService>().Received(1)
            .AutoAddSeatsAsync(sponsoringOrg, 1);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task CreateSponsorship_CreatesAdminInitiatedSponsorship_ThrowsWhenCannotAutoscale(
        OrganizationUserType organizationUserType,
        Organization sponsoringOrg, OrganizationUser sponsoringOrgUser, User user, string sponsoredEmail,
        string friendlyName, Guid sponsorshipId, Guid currentUserId, string notes, SutProvider<CreateSponsorshipCommand> sutProvider)
    {
        sponsoringOrg.PlanType = PlanType.EnterpriseAnnually;
        sponsoringOrg.UseAdminSponsoredFamilies = true;
        sponsoringOrg.Seats = 10;
        sponsoringOrgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(sponsoringOrgUser.UserId!.Value).Returns(user);
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>().WhenForAnyArgs(x => x.UpsertAsync(null!)).Do(callInfo =>
        {
            var sponsorship = callInfo.Arg<OrganizationSponsorship>();
            sponsorship.Id = sponsorshipId;
        });
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(currentUserId);
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns([
            new()
            {
                Id = sponsoringOrg.Id,
                Permissions = new Permissions { ManageUsers = true },
                Type = organizationUserType
            }
        ]);

        // Setup for checking available seats - organization has no available seats
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(sponsoringOrg.Id)
            .Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 10
            });

        // Setup for checking if can scale - cannot scale
        var failureReason = "Seat limit has been reached.";
        sutProvider.GetDependency<IOrganizationService>()
            .CanScaleAsync(sponsoringOrg, 1)
            .Returns((false, failureReason));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName, true, notes));

        Assert.Equal(failureReason, exception.Message);
    }
}
