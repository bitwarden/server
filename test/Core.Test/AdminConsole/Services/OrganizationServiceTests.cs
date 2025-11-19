using System.Text.Json;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Core.Test.Billing.Mocks;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Fakes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;
using Organization = Bit.Core.AdminConsole.Entities.Organization;
using OrganizationUser = Bit.Core.Entities.OrganizationUser;
using OrganizationUserInvite = Bit.Core.Models.Business.OrganizationUserInvite;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class OrganizationServiceTests
{
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory = new FakeDataProtectorTokenFactory<OrgUserInviteTokenable>();

    [Theory]
    [OrganizationInviteCustomize(InviteeUserType = OrganizationUserType.User,
         InvitorUserType = OrganizationUserType.Owner), OrganizationCustomize, BitAutoData]
    public async Task InviteUsers_NoEmails_Throws(Organization organization, OrganizationUser invitor,
        OrganizationUserInvite invite, SutProvider<OrganizationService> sutProvider)
    {
        invite.Emails = null;
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, systemUser: null, new (OrganizationUserInvite, string)[] { (invite, null) }));
    }

    [Theory]
    [OrganizationInviteCustomize, OrganizationCustomize, BitAutoData]
    public async Task InviteUsers_DuplicateEmails_PassesWithoutDuplicates(Organization organization, OrganizationUser invitor,
                [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        OrganizationUserInvite invite, SutProvider<OrganizationService> sutProvider)
    {
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        invite.Emails = invite.Emails.Append(invite.Emails.First());
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        organizationUserRepository.GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new[] { owner });

        // Must set guids in order for dictionary of guids to not throw aggregate exceptions
        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);

        await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, systemUser: null, new (OrganizationUserInvite, string)[] { (invite, null) });

        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>().Received(1)
            .SendInvitesAsync(Arg.Is<SendInvitesRequest>(request =>
                request.Users.DistinctBy(x => x.Email).Count() == invite.Emails.Distinct().Count() &&
                request.Organization == organization));

    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.Admin,
        InvitorUserType = OrganizationUserType.Owner
    ), OrganizationCustomize, BitAutoData]
    public async Task InviteUsers_NoOwner_Throws(Organization organization, OrganizationUser invitor,
        OrganizationUserInvite invite, SutProvider<OrganizationService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, systemUser: null, new (OrganizationUserInvite, string)[] { (invite, null) }));
        Assert.Contains("Organization must have at least one confirmed owner.", exception.Message);
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.Owner,
        InvitorUserType = OrganizationUserType.Admin
    ), OrganizationCustomize, BitAutoData]
    public async Task InviteUsers_NonOwnerConfiguringOwner_Throws(Organization organization, OrganizationUserInvite invite,
        OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
    {
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        currentContext.OrganizationAdmin(organization.Id).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, systemUser: null, new (OrganizationUserInvite, string)[] { (invite, null) }));
        Assert.Contains("only an owner", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.Custom,
        InvitorUserType = OrganizationUserType.User
    ), OrganizationCustomize, BitAutoData]
    public async Task InviteUsers_NonAdminConfiguringAdmin_Throws(Organization organization, OrganizationUserInvite invite,
        OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
    {
        organization.UseCustomPermissions = true;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        currentContext.OrganizationUser(organization.Id).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, systemUser: null, new (OrganizationUserInvite, string)[] { (invite, null) }));
        Assert.Contains("your account does not have permission to manage users", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [OrganizationInviteCustomize(
         InviteeUserType = OrganizationUserType.Custom,
         InvitorUserType = OrganizationUserType.Admin
     ), OrganizationCustomize, BitAutoData]
    public async Task InviteUsers_WithCustomType_WhenUseCustomPermissionsIsFalse_Throws(Organization organization, OrganizationUserInvite invite,
        OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
    {
        organization.UseCustomPermissions = false;

        invite.Permissions = null;
        invitor.Status = OrganizationUserStatusType.Confirmed;
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        organizationUserRepository.GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new[] { invitor });
        currentContext.OrganizationOwner(organization.Id).Returns(true);
        currentContext.ManageUsers(organization.Id).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, systemUser: null, new (OrganizationUserInvite, string)[] { (invite, null) }));
        Assert.Contains("to enable custom permissions", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [OrganizationInviteCustomize(
         InviteeUserType = OrganizationUserType.Custom,
         InvitorUserType = OrganizationUserType.Admin
     ), OrganizationCustomize, BitAutoData]
    public async Task InviteUsers_WithCustomType_WhenUseCustomPermissionsIsTrue_Passes(Organization organization, OrganizationUserInvite invite,
        OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
    {
        organization.Seats = 10;
        organization.UseCustomPermissions = true;

        invite.Permissions = null;
        invitor.Status = OrganizationUserStatusType.Confirmed;
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organization.Id, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });

        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);

        currentContext.OrganizationOwner(organization.Id).Returns(true);
        currentContext.ManageUsers(organization.Id).Returns(true);

        await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, systemUser: null, new (OrganizationUserInvite, string)[] { (invite, null) });
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.User)]
    public async Task InviteUsers_WithNonCustomType_WhenUseCustomPermissionsIsFalse_Passes(OrganizationUserType inviteUserType, Organization organization, OrganizationUserInvite invite,
        OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
    {
        organization.Seats = 10;
        organization.UseCustomPermissions = false;

        invite.Type = inviteUserType;
        invite.Permissions = null;
        invitor.Status = OrganizationUserStatusType.Confirmed;
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organization.Id, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);

        currentContext.OrganizationOwner(organization.Id).Returns(true);
        currentContext.ManageUsers(organization.Id).Returns(true);

        await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, systemUser: null, new (OrganizationUserInvite, string)[] { (invite, null) });
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.User,
        InvitorUserType = OrganizationUserType.Custom
    ), OrganizationCustomize, BitAutoData]
    public async Task InviteUsers_CustomUserWithoutManageUsersConfiguringUser_Throws(Organization organization, OrganizationUserInvite invite,
        OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
    {
        invitor.Permissions = JsonSerializer.Serialize(new Permissions() { ManageUsers = false },
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);
        currentContext.OrganizationCustom(organization.Id).Returns(true);
        currentContext.ManageUsers(organization.Id).Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, systemUser: null, new (OrganizationUserInvite, string)[] { (invite, null) }));
        Assert.Contains("account does not have permission", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.Admin,
        InvitorUserType = OrganizationUserType.Custom
    ), OrganizationCustomize, BitAutoData]
    public async Task InviteUsers_CustomUserConfiguringAdmin_Throws(Organization organization, OrganizationUserInvite invite,
        OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
    {
        invitor.Permissions = JsonSerializer.Serialize(new Permissions() { ManageUsers = true },
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        currentContext.OrganizationCustom(organization.Id).Returns(true);
        currentContext.ManageUsers(organization.Id).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, systemUser: null, new (OrganizationUserInvite, string)[] { (invite, null) }));
        Assert.Contains("can not manage admins", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.User,
        InvitorUserType = OrganizationUserType.Owner
    ), OrganizationCustomize, BitAutoData]
    public async Task InviteUsers_NoPermissionsObject_Passes(Organization organization, OrganizationUserInvite invite,
        OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
    {
        invite.Permissions = null;
        invitor.Status = OrganizationUserStatusType.Confirmed;
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organization.Id, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);

        currentContext.OrganizationOwner(organization.Id).Returns(true);
        currentContext.ManageUsers(organization.Id).Returns(true);

        await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, systemUser: null, new (OrganizationUserInvite, string)[] { (invite, null) });
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.User,
        InvitorUserType = OrganizationUserType.Custom
    ), OrganizationCustomize, BitAutoData]
    public async Task InviteUser_Passes(Organization organization, OrganizationUserInvite invite, string externalId,
        OrganizationUser invitor,
        SutProvider<OrganizationService> sutProvider)
    {
        // This method is only used to invite 1 user at a time
        invite.Emails = new[] { invite.Emails.First() };

        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        InviteUser_ArrangeCurrentContextPermissions(organization, sutProvider);

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        // Mock tokenable factory to return a token that expires in 5 days
        // sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
        //     .CreateToken(Arg.Any<OrganizationUser>())
        //     .Returns(
        //         info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
        //         {
        //             ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
        //         }
        //     );

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organization.Id, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);
        SetupOrgUserRepositoryCreateAsyncMock(organizationUserRepository);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        await sutProvider.Sut.InviteUserAsync(organization.Id, invitor.UserId, systemUser: null, invite, externalId);

        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>().Received(1)
            .SendInvitesAsync(Arg.Is<SendInvitesRequest>(request =>
                request.Users.Length == 1 &&
                request.Organization == organization));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>());
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.User,
        InvitorUserType = OrganizationUserType.Custom
    ), OrganizationCustomize, BitAutoData]
    public async Task InviteUser_InvitingMoreThanOneUser_Throws(Organization organization, OrganizationUserInvite invite, string externalId,
        OrganizationUser invitor,
        SutProvider<OrganizationService> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.InviteUserAsync(organization.Id, invitor.UserId, systemUser: null, invite, externalId));
        Assert.Contains("This method can only be used to invite a single user.", exception.Message);

        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
            .SendOrganizationInviteEmailsAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceive()
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, EventSystemUser, DateTime?)>>());
        await sutProvider.GetDependency<IEventService>().DidNotReceive()
            .LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>());
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.User,
        InvitorUserType = OrganizationUserType.Custom
    ), OrganizationCustomize, BitAutoData]
    public async Task InviteUser_UserAlreadyInvited_Throws(Organization organization, OrganizationUserInvite invite, string externalId,
        OrganizationUser invitor,
        SutProvider<OrganizationService> sutProvider)
    {
        // This method is only used to invite 1 user at a time
        invite.Emails = new[] { invite.Emails.First() };

        // The user has already been invited
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .SelectKnownEmailsAsync(organization.Id, Arg.Any<IEnumerable<string>>(), false)
            .Returns(new List<string> { invite.Emails.First() });

        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        InviteUser_ArrangeCurrentContextPermissions(organization, sutProvider);

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organization.Id, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);
        SetupOrgUserRepositoryCreateAsyncMock(organizationUserRepository);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut
            .InviteUserAsync(organization.Id, invitor.UserId, systemUser: null, invite, externalId));
        Assert.Contains("This user has already been invited", exception.Message);

        // SendOrganizationInvitesCommand and EventService are still called, but with no OrgUsers
        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>().Received(1)
            .SendInvitesAsync(Arg.Is<SendInvitesRequest>(info =>
                info.Organization == organization &&
                info.Users.Length == 0));
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventsAsync(Arg.Is<IEnumerable<(OrganizationUser, EventType, DateTime?)>>(events => !events.Any()));
    }

    private void InviteUser_ArrangeCurrentContextPermissions(Organization organization, SutProvider<OrganizationService> sutProvider)
    {
        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        currentContext.ManageUsers(organization.Id).Returns(true);
        currentContext.AccessReports(organization.Id).Returns(true);
        currentContext.ManageGroups(organization.Id).Returns(true);
        currentContext.ManagePolicies(organization.Id).Returns(true);
        currentContext.ManageScim(organization.Id).Returns(true);
        currentContext.ManageSso(organization.Id).Returns(true);
        currentContext.AccessEventLogs(organization.Id).Returns(true);
        currentContext.AccessImportExport(organization.Id).Returns(true);
        currentContext.EditAnyCollection(organization.Id).Returns(true);
        currentContext.ManageResetPassword(organization.Id).Returns(true);
        currentContext.GetOrganization(organization.Id)
            .Returns(new CurrentContextOrganization()
            {
                Permissions = new Permissions
                {
                    CreateNewCollections = true,
                    DeleteAnyCollection = true
                }
            });
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.User,
        InvitorUserType = OrganizationUserType.Custom
    ), OrganizationCustomize, BitAutoData]
    public async Task InviteUsers_Passes(Organization organization, IEnumerable<(OrganizationUserInvite invite, string externalId)> invites,
        OrganizationUser invitor,
        SutProvider<OrganizationService> sutProvider)
    {
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        InviteUser_ArrangeCurrentContextPermissions(organization, sutProvider);

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organization.Id, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);
        SetupOrgUserRepositoryCreateAsyncMock(organizationUserRepository);

        await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, systemUser: null, invites);

        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>().Received(1)
            .SendInvitesAsync(Arg.Is<SendInvitesRequest>(info =>
                info.Organization == organization &&
                info.Users.Length == invites.SelectMany(x => x.invite.Emails).Distinct().Count()));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>());
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.User,
        InvitorUserType = OrganizationUserType.Custom
    ), OrganizationCustomize, BitAutoData]
    public async Task InviteUsers_WithEventSystemUser_Passes(Organization organization, EventSystemUser eventSystemUser, IEnumerable<(OrganizationUserInvite invite, string externalId)> invites,
        OrganizationUser invitor,
        SutProvider<OrganizationService> sutProvider)
    {
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        invitor.Permissions = JsonSerializer.Serialize(new Permissions() { ManageUsers = true },
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organization.Id, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        SetupOrgUserRepositoryCreateAsyncMock(organizationUserRepository);
        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);

        currentContext.ManageUsers(organization.Id).Returns(true);

        await sutProvider.Sut.InviteUsersAsync(organization.Id, invitingUserId: null, eventSystemUser, invites);

        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>().Received(1)
            .SendInvitesAsync(Arg.Is<SendInvitesRequest>(info =>
                info.Users.Length == invites.SelectMany(i => i.invite.Emails).Count() &&
                info.Organization == organization));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, EventSystemUser, DateTime?)>>());
    }

    [Theory, BitAutoData, OrganizationCustomize, OrganizationInviteCustomize]
    public async Task InviteUsers_WithSecretsManager_Passes(Organization organization,
        IEnumerable<(OrganizationUserInvite invite, string externalId)> invites,
        OrganizationUser savingUser, SutProvider<OrganizationService> sutProvider)
    {
        organization.PlanType = PlanType.EnterpriseAnnually;
        InviteUserHelper_ArrangeValidPermissions(organization, savingUser, sutProvider);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });

        // Set up some invites to grant access to SM
        invites.First().invite.AccessSecretsManager = true;
        var invitedSmUsers = invites.First().invite.Emails.Count();
        foreach (var (invite, externalId) in invites.Skip(1))
        {
            invite.AccessSecretsManager = false;
        }

        // Assume we need to add seats for all invited SM users
        sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>()
            .CountNewSmSeatsRequiredAsync(organization.Id, invitedSmUsers).Returns(invitedSmUsers);

        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);
        SetupOrgUserRepositoryCreateAsyncMock(organizationUserRepository);

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(organization.PlanType).Returns(MockPlans.Get(organization.PlanType));

        await sutProvider.Sut.InviteUsersAsync(organization.Id, savingUser.Id, systemUser: null, invites);

        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>().Received(1)
            .UpdateSubscriptionAsync(Arg.Is<SecretsManagerSubscriptionUpdate>(update =>
                update.SmSeats == organization.SmSeats + invitedSmUsers &&
                !update.SmServiceAccountsChanged &&
                !update.MaxAutoscaleSmSeatsChanged &&
                !update.MaxAutoscaleSmSeatsChanged));
    }

    [Theory, BitAutoData, OrganizationCustomize, OrganizationInviteCustomize]
    public async Task InviteUsers_WithSecretsManager_WhenErrorIsThrown_RevertsAutoscaling(Organization organization,
        IEnumerable<(OrganizationUserInvite invite, string externalId)> invites,
        OrganizationUser savingUser, SutProvider<OrganizationService> sutProvider)
    {
        var initialSmSeats = organization.SmSeats;
        InviteUserHelper_ArrangeValidPermissions(organization, savingUser, sutProvider);

        // Set up some invites to grant access to SM
        invites.First().invite.AccessSecretsManager = true;
        var invitedSmUsers = invites.First().invite.Emails.Count();
        foreach (var (invite, externalId) in invites.Skip(1))
        {
            invite.AccessSecretsManager = false;
        }

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });

        // Assume we need to add seats for all invited SM users
        sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>()
            .CountNewSmSeatsRequiredAsync(organization.Id, invitedSmUsers).Returns(invitedSmUsers);

        // Mock SecretsManagerSubscriptionUpdateCommand to actually change the organization's subscription in memory
        sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .UpdateSubscriptionAsync(Arg.Any<SecretsManagerSubscriptionUpdate>())
            .ReturnsForAnyArgs(Task.FromResult(0)).AndDoes(x => organization.SmSeats += invitedSmUsers);

        sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .SendInvitesAsync(Arg.Any<SendInvitesRequest>()).ThrowsAsync<Exception>();

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(organization.PlanType)
            .Returns(MockPlans.Get(organization.PlanType));

        await Assert.ThrowsAsync<AggregateException>(async () =>
            await sutProvider.Sut.InviteUsersAsync(organization.Id, savingUser.Id, systemUser: null, invites));

        // OrgUser is reverted
        // Note: we don't know what their guids are so comparing length is the best we can do
        var invitedEmails = invites.SelectMany(i => i.invite.Emails);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).DeleteManyAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == invitedEmails.Count()));

        Received.InOrder(() =>
        {
            // Initial autoscaling
            sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
                .UpdateSubscriptionAsync(Arg.Is<SecretsManagerSubscriptionUpdate>(update =>
                    update.SmSeats == initialSmSeats + invitedSmUsers &&
                    !update.SmServiceAccountsChanged &&
                    !update.MaxAutoscaleSmSeatsChanged &&
                    !update.MaxAutoscaleSmSeatsChanged));

            // Revert autoscaling
            sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
                .UpdateSubscriptionAsync(Arg.Is<SecretsManagerSubscriptionUpdate>(update =>
                    update.SmSeats == initialSmSeats &&
                    !update.SmServiceAccountsChanged &&
                    !update.MaxAutoscaleSmSeatsChanged &&
                    !update.MaxAutoscaleSmSeatsChanged));
        });
    }

    private void InviteUserHelper_ArrangeValidPermissions(Organization organization, OrganizationUser savingUser,
    SutProvider<OrganizationService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
    }

    [Theory]
    [PaidOrganizationCustomize(CheckedPlanType = PlanType.EnterpriseAnnually)]
    [BitAutoData("Cannot set max seat autoscaling below seat count", 1, 0, 2, 2)]
    [BitAutoData("Cannot set max seat autoscaling below seat count", 4, -1, 6, 6)]
    public async Task Enterprise_UpdateMaxSeatAutoscaling_BadInputThrows(string expectedMessage,
        int? maxAutoscaleSeats, int seatAdjustment, int? currentSeats, int? currentMaxAutoscaleSeats,
        Organization organization, SutProvider<OrganizationService> sutProvider)
        => await UpdateSubscription_BadInputThrows(expectedMessage, maxAutoscaleSeats, seatAdjustment, currentSeats,
            currentMaxAutoscaleSeats, organization, sutProvider);
    [Theory]
    [FreeOrganizationCustomize]
    [BitAutoData("Your plan does not allow seat autoscaling", 10, 0, null, null)]
    public async Task Free_UpdateMaxSeatAutoscaling_BadInputThrows(string expectedMessage,
        int? maxAutoscaleSeats, int seatAdjustment, int? currentSeats, int? currentMaxAutoscaleSeats,
        Organization organization, SutProvider<OrganizationService> sutProvider)
        => await UpdateSubscription_BadInputThrows(expectedMessage, maxAutoscaleSeats, seatAdjustment, currentSeats,
            currentMaxAutoscaleSeats, organization, sutProvider);

    private async Task UpdateSubscription_BadInputThrows(string expectedMessage,
        int? maxAutoscaleSeats, int seatAdjustment, int? currentSeats, int? currentMaxAutoscaleSeats,
        Organization organization, SutProvider<OrganizationService> sutProvider)
    {
        organization.Seats = currentSeats;
        organization.MaxAutoscaleSeats = currentMaxAutoscaleSeats;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(organization.PlanType)
            .Returns(MockPlans.Get(organization.PlanType));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscription(organization.Id,
            seatAdjustment, maxAutoscaleSeats));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateSubscription_NoOrganization_Throws(Guid organizationId, SutProvider<OrganizationService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns((Organization)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateSubscription(organizationId, 0, null));
    }

    [Theory, SecretsManagerOrganizationCustomize]
    [BitAutoData("You cannot have more Secrets Manager seats than Password Manager seats.", -1)]
    public async Task UpdateSubscription_PmSeatAdjustmentLessThanSmSeats_Throws(string expectedMessage,
        int seatAdjustment, Organization organization, SutProvider<OrganizationService> sutProvider)
    {
        organization.Seats = 100;
        organization.SmSeats = 100;

        sutProvider.GetDependency<IPricingClient>().GetPlanOrThrow(organization.PlanType)
            .Returns(MockPlans.Get(organization.PlanType));
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var actual = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscription(organization.Id, seatAdjustment, null));
        Assert.Contains(expectedMessage, actual.Message);
    }

    [Theory, PaidOrganizationCustomize]
    [BitAutoData(0, 100, null, true, "")]
    [BitAutoData(0, 100, 100, true, "")]
    [BitAutoData(0, null, 100, true, "")]
    [BitAutoData(1, 100, null, true, "")]
    [BitAutoData(1, 100, 100, false, "Seat limit has been reached")]
    public async Task CanScaleAsync(int seatsToAdd, int? currentSeats, int? maxAutoscaleSeats,
        bool expectedResult, string expectedFailureMessage, Organization organization,
        SutProvider<OrganizationService> sutProvider)
    {
        organization.Seats = currentSeats;
        organization.MaxAutoscaleSeats = maxAutoscaleSeats;
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organization.Id).ReturnsNull();

        var (result, failureMessage) = await sutProvider.Sut.CanScaleAsync(organization, seatsToAdd);

        if (expectedFailureMessage == string.Empty)
        {
            Assert.Empty(failureMessage);
        }
        else
        {
            Assert.Contains(expectedFailureMessage, failureMessage);
        }
        Assert.Equal(expectedResult, result);
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task CanScaleAsync_FailsOnSelfHosted(Organization organization,
        SutProvider<OrganizationService> sutProvider)
    {
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(true);
        var (result, failureMessage) = await sutProvider.Sut.CanScaleAsync(organization, 10);

        Assert.False(result);
        Assert.Contains("Cannot autoscale on self-hosted instance", failureMessage);
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task CanScaleAsync_FailsOnResellerManagedOrganization(
        Organization organization,
        SutProvider<OrganizationService> sutProvider)
    {
        var provider = new Provider
        {
            Enabled = true,
            Type = ProviderType.Reseller
        };

        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organization.Id).Returns(provider);

        var (result, failureMessage) = await sutProvider.Sut.CanScaleAsync(organization, 10);

        Assert.False(result);
        Assert.Contains("Seat limit has been reached. Contact your provider to purchase additional seats.", failureMessage);
    }


    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsStarter)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenNoSecretsManagerSeats(PlanType planType, SutProvider<OrganizationService> sutProvider)
    {
        var plan = MockPlans.Get(planType);
        var signup = new OrganizationUpgrade
        {
            UseSecretsManager = true,
            AdditionalSmSeats = 0,
            AdditionalServiceAccounts = 5,
            AdditionalSeats = 2
        };

        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup));
        Assert.Contains("You do not have any Secrets Manager seats!", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenSubtractingSeats(PlanType planType, SutProvider<OrganizationService> sutProvider)
    {
        var plan = MockPlans.Get(planType);
        var signup = new OrganizationUpgrade
        {
            UseSecretsManager = true,
            AdditionalSmSeats = -1,
            AdditionalServiceAccounts = 5
        };
        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup));
        Assert.Contains("You can't subtract Secrets Manager seats!", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenPlanDoesNotAllowAdditionalServiceAccounts(
        PlanType planType,
        SutProvider<OrganizationService> sutProvider)
    {
        var plan = MockPlans.Get(planType);
        var signup = new OrganizationUpgrade
        {
            UseSecretsManager = true,
            AdditionalSmSeats = 2,
            AdditionalServiceAccounts = 5,
            AdditionalSeats = 3
        };
        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup));
        Assert.Contains("Plan does not allow additional Machine Accounts.", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenMoreSeatsThanPasswordManagerSeats(PlanType planType, SutProvider<OrganizationService> sutProvider)
    {
        var plan = MockPlans.Get(planType);
        var signup = new OrganizationUpgrade
        {
            UseSecretsManager = true,
            AdditionalSmSeats = 4,
            AdditionalServiceAccounts = 5,
            AdditionalSeats = 3
        };
        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup));
        Assert.Contains("You cannot have more Secrets Manager seats than Password Manager seats.", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsStarter)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenSubtractingServiceAccounts(
        PlanType planType,
        SutProvider<OrganizationService> sutProvider)
    {
        var plan = MockPlans.Get(planType);
        var signup = new OrganizationUpgrade
        {
            UseSecretsManager = true,
            AdditionalSmSeats = 4,
            AdditionalServiceAccounts = -5,
            AdditionalSeats = 5
        };
        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup));
        Assert.Contains("You can't subtract Machine Accounts!", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenPlanDoesNotAllowAdditionalUsers(
        PlanType planType,
        SutProvider<OrganizationService> sutProvider)
    {
        var plan = MockPlans.Get(planType);
        var signup = new OrganizationUpgrade
        {
            UseSecretsManager = true,
            AdditionalSmSeats = 2,
            AdditionalServiceAccounts = 0,
            AdditionalSeats = 5
        };
        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup));
        Assert.Contains("Plan does not allow additional users.", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsStarter)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ValidPlan_NoExceptionThrown(
        PlanType planType,
        SutProvider<OrganizationService> sutProvider)
    {
        var plan = MockPlans.Get(planType);
        var signup = new OrganizationUpgrade
        {
            UseSecretsManager = true,
            AdditionalSmSeats = 2,
            AdditionalServiceAccounts = 0,
            AdditionalSeats = 4
        };

        sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup);
    }

    [Theory]
    [OrganizationInviteCustomize(
         InviteeUserType = OrganizationUserType.Custom,
         InvitorUserType = OrganizationUserType.Custom
     ), BitAutoData]
    public async Task ValidateOrganizationUserUpdatePermissions_WithCustomPermission_WhenSavingUserHasCustomPermission_Passes(
        CurrentContextOrganization organization,
        OrganizationUserInvite organizationUserInvite,
        SutProvider<OrganizationService> sutProvider)
    {
        var invitePermissions = new Permissions { AccessReports = true };
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().AccessReports(organization.Id).Returns(true);

        await sutProvider.Sut.ValidateOrganizationUserUpdatePermissions(organization.Id, organizationUserInvite.Type.Value, null, invitePermissions);
    }

    [Theory]
    [OrganizationInviteCustomize(
         InviteeUserType = OrganizationUserType.Owner,
         InvitorUserType = OrganizationUserType.Admin
     ), BitAutoData]
    public async Task ValidateOrganizationUserUpdatePermissions_WithAdminAddingOwner_Throws(
        Guid organizationId,
        OrganizationUserInvite organizationUserInvite,
        SutProvider<OrganizationService> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateOrganizationUserUpdatePermissions(organizationId, organizationUserInvite.Type.Value, null, organizationUserInvite.Permissions));

        Assert.Contains("only an owner can configure another owner's account.", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.Admin,
        InvitorUserType = OrganizationUserType.Owner
    ), BitAutoData]
    public async Task ValidateOrganizationUserUpdatePermissions_WithoutManageUsersPermission_Throws(
        Guid organizationId,
        OrganizationUserInvite organizationUserInvite,
        SutProvider<OrganizationService> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateOrganizationUserUpdatePermissions(organizationId, organizationUserInvite.Type.Value, null, organizationUserInvite.Permissions));

        Assert.Contains("your account does not have permission to manage users.", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [OrganizationInviteCustomize(
         InviteeUserType = OrganizationUserType.Admin,
         InvitorUserType = OrganizationUserType.Custom
     ), BitAutoData]
    public async Task ValidateOrganizationUserUpdatePermissions_WithCustomAddingAdmin_Throws(
        Guid organizationId,
        OrganizationUserInvite organizationUserInvite,
        SutProvider<OrganizationService> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organizationId).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateOrganizationUserUpdatePermissions(organizationId, organizationUserInvite.Type.Value, null, organizationUserInvite.Permissions));

        Assert.Contains("custom users can not manage admins or owners.", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [OrganizationInviteCustomize(
         InviteeUserType = OrganizationUserType.Custom,
         InvitorUserType = OrganizationUserType.Custom
     ), BitAutoData]
    public async Task ValidateOrganizationUserUpdatePermissions_WithCustomAddingUser_WithoutPermissions_Throws(
        Guid organizationId,
        OrganizationUserInvite organizationUserInvite,
        SutProvider<OrganizationService> sutProvider)
    {
        var invitePermissions = new Permissions { AccessReports = true };
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organizationId).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().AccessReports(organizationId).Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateOrganizationUserUpdatePermissions(organizationId, organizationUserInvite.Type.Value, null, invitePermissions));

        Assert.Contains("custom users can only grant the same custom permissions that they have.", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.User)]
    public async Task ValidateOrganizationCustomPermissionsEnabledAsync_WithNotCustomType_IsValid(
        OrganizationUserType newType,
        Guid organizationId,
        SutProvider<OrganizationService> sutProvider)
    {
        await sutProvider.Sut.ValidateOrganizationCustomPermissionsEnabledAsync(organizationId, newType);
    }

    [Theory, BitAutoData]
    public async Task ValidateOrganizationCustomPermissionsEnabledAsync_NotExistingOrg_ThrowsNotFound(
        Guid organizationId,
        SutProvider<OrganizationService> sutProvider)
    {
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.ValidateOrganizationCustomPermissionsEnabledAsync(organizationId, OrganizationUserType.Custom));
    }

    [Theory, BitAutoData]
    public async Task ValidateOrganizationCustomPermissionsEnabledAsync_WithUseCustomPermissionsDisabled_ThrowsBadRequest(
        Organization organization,
        SutProvider<OrganizationService> sutProvider)
    {
        organization.UseCustomPermissions = false;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateOrganizationCustomPermissionsEnabledAsync(organization.Id, OrganizationUserType.Custom));

        Assert.Contains("to enable custom permissions", exception.Message.ToLowerInvariant());
    }

    [Theory, BitAutoData]
    public async Task ValidateOrganizationCustomPermissionsEnabledAsync_WithUseCustomPermissionsEnabled_IsValid(
        Organization organization,
        SutProvider<OrganizationService> sutProvider)
    {
        organization.UseCustomPermissions = true;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        await sutProvider.Sut.ValidateOrganizationCustomPermissionsEnabledAsync(organization.Id, OrganizationUserType.Custom);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenValidOrganization_AndUpdateBillingIsTrue_UpdateStripeCustomerAndOrganization(Organization organization, SutProvider<OrganizationService> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var applicationCacheService = sutProvider.GetDependency<IApplicationCacheService>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        var eventService = sutProvider.GetDependency<IEventService>();

        var requestOptionsReturned = new CustomerUpdateOptions
        {
            Email = organization.BillingEmail,
            Description = organization.DisplayBusinessName(),
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                // This overwrites the existing custom fields for this organization
                CustomFields =
                [
                    new CustomerInvoiceSettingsCustomFieldOptions
                    {
                        Name = organization.SubscriberType(),
                        Value = organization.DisplayName()[..30]
                    }
                ]
            },
        };
        organizationRepository
            .GetByIdentifierAsync(organization.Identifier!)
            .Returns(organization);

        // Act
        await sutProvider.Sut.UpdateAsync(organization, updateBilling: true);

        // Assert
        await organizationRepository
            .Received(1)
            .GetByIdentifierAsync(Arg.Is<string>(id => id == organization.Identifier));
        await stripeAdapter
            .Received(1)
            .CustomerUpdateAsync(
                Arg.Is<string>(id => id == organization.GatewayCustomerId),
                Arg.Is<CustomerUpdateOptions>(options => options.Email == requestOptionsReturned.Email
                                                         && options.Description == requestOptionsReturned.Description
                                                         && options.InvoiceSettings.CustomFields.First().Name == requestOptionsReturned.InvoiceSettings.CustomFields.First().Name
                                                         && options.InvoiceSettings.CustomFields.First().Value == requestOptionsReturned.InvoiceSettings.CustomFields.First().Value)); ;
        await organizationRepository
            .Received(1)
            .ReplaceAsync(Arg.Is<Organization>(org => org == organization));
        await applicationCacheService
            .Received(1)
            .UpsertOrganizationAbilityAsync(Arg.Is<Organization>(org => org == organization));
        await eventService
            .Received(1)
            .LogOrganizationEventAsync(Arg.Is<Organization>(org => org == organization),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenValidOrganization_AndUpdateBillingIsFalse_UpdateOrganization(Organization organization, SutProvider<OrganizationService> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var applicationCacheService = sutProvider.GetDependency<IApplicationCacheService>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();
        var eventService = sutProvider.GetDependency<IEventService>();

        organizationRepository
            .GetByIdentifierAsync(organization.Identifier!)
            .Returns(organization);

        // Act
        await sutProvider.Sut.UpdateAsync(organization, updateBilling: false);

        // Assert
        await organizationRepository
            .Received(1)
            .GetByIdentifierAsync(Arg.Is<string>(id => id == organization.Identifier));
        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
        await organizationRepository
            .Received(1)
            .ReplaceAsync(Arg.Is<Organization>(org => org == organization));
        await applicationCacheService
            .Received(1)
            .UpsertOrganizationAbilityAsync(Arg.Is<Organization>(org => org == organization));
        await eventService
            .Received(1)
            .LogOrganizationEventAsync(Arg.Is<Organization>(org => org == organization),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenOrganizationHasNoId_ThrowsApplicationException(Organization organization, SutProvider<OrganizationService> sutProvider)
    {
        // Arrange
        organization.Id = Guid.Empty;

        // Act/Assert
        var exception = await Assert.ThrowsAnyAsync<ApplicationException>(() => sutProvider.Sut.UpdateAsync(organization));
        Assert.Equal("Cannot create org this way. Call SignUpAsync.", exception.Message);

    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenIdentifierAlreadyExistsForADifferentOrganization_ThrowsBadRequestException(Organization organization, SutProvider<OrganizationService> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var differentOrganization = new Organization { Id = Guid.NewGuid() };

        organizationRepository
            .GetByIdentifierAsync(organization.Identifier!)
            .Returns(differentOrganization);

        // Act/Assert
        var exception = await Assert.ThrowsAnyAsync<BadRequestException>(() => sutProvider.Sut.UpdateAsync(organization));
        Assert.Equal("Identifier already in use by another organization.", exception.Message);

        await organizationRepository
            .Received(1)
            .GetByIdentifierAsync(Arg.Is<string>(id => id == organization.Identifier));
    }

    [Theory]
    [BitAutoData(false, true, false, true)]
    [BitAutoData(true, false, true, false)]
    public async Task UpdateCollectionManagementSettingsAsync_WhenSettingsChanged_LogsSpecificEvents(
        bool newLimitCollectionCreation,
        bool newLimitCollectionDeletion,
        bool newLimitItemDeletion,
        bool newAllowAdminAccessToAllCollectionItems,
        Organization existingOrganization, SutProvider<OrganizationService> sutProvider)
    {
        // Arrange
        existingOrganization.LimitCollectionCreation = false;
        existingOrganization.LimitCollectionDeletion = false;
        existingOrganization.LimitItemDeletion = false;
        existingOrganization.AllowAdminAccessToAllCollectionItems = false;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(existingOrganization.Id)
            .Returns(existingOrganization);

        var settings = new OrganizationCollectionManagementSettings
        {
            LimitCollectionCreation = newLimitCollectionCreation,
            LimitCollectionDeletion = newLimitCollectionDeletion,
            LimitItemDeletion = newLimitItemDeletion,
            AllowAdminAccessToAllCollectionItems = newAllowAdminAccessToAllCollectionItems
        };

        // Act
        await sutProvider.Sut.UpdateCollectionManagementSettingsAsync(existingOrganization.Id, settings);

        // Assert
        var eventService = sutProvider.GetDependency<IEventService>();
        if (newLimitCollectionCreation)
        {
            await eventService.Received(1).LogOrganizationEventAsync(
                Arg.Is<Organization>(org => org.Id == existingOrganization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_CollectionManagement_LimitCollectionCreationEnabled));
        }
        else
        {
            await eventService.DidNotReceive().LogOrganizationEventAsync(
                Arg.Is<Organization>(org => org.Id == existingOrganization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_CollectionManagement_LimitCollectionCreationEnabled));
        }

        if (newLimitCollectionDeletion)
        {
            await eventService.Received(1).LogOrganizationEventAsync(
                Arg.Is<Organization>(org => org.Id == existingOrganization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_CollectionManagement_LimitCollectionDeletionEnabled));
        }
        else
        {
            await eventService.DidNotReceive().LogOrganizationEventAsync(
                Arg.Is<Organization>(org => org.Id == existingOrganization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_CollectionManagement_LimitCollectionDeletionEnabled));
        }

        if (newLimitItemDeletion)
        {
            await eventService.Received(1).LogOrganizationEventAsync(
                Arg.Is<Organization>(org => org.Id == existingOrganization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_CollectionManagement_LimitItemDeletionEnabled));
        }
        else
        {
            await eventService.DidNotReceive().LogOrganizationEventAsync(
                Arg.Is<Organization>(org => org.Id == existingOrganization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_CollectionManagement_LimitItemDeletionEnabled));
        }

        if (newAllowAdminAccessToAllCollectionItems)
        {
            await eventService.Received(1).LogOrganizationEventAsync(
                Arg.Is<Organization>(org => org.Id == existingOrganization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_CollectionManagement_AllowAdminAccessToAllCollectionItemsEnabled));
        }
        else
        {
            await eventService.DidNotReceive().LogOrganizationEventAsync(
                Arg.Is<Organization>(org => org.Id == existingOrganization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_CollectionManagement_AllowAdminAccessToAllCollectionItemsEnabled));
        }
    }

    [Theory, BitAutoData]
    public async Task UpdateCollectionManagementSettingsAsync_WhenOrganizationNotFound_ThrowsNotFoundException(
        Guid organizationId, OrganizationCollectionManagementSettings settings, SutProvider<OrganizationService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns((Organization)null);

        // Act/Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateCollectionManagementSettingsAsync(organizationId, settings));

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .GetByIdAsync(organizationId);
    }

    // Must set real guids in order for dictionary of guids to not throw aggregate exceptions
    private void SetupOrgUserRepositoryCreateManyAsyncMock(IOrganizationUserRepository organizationUserRepository)
    {
        organizationUserRepository.CreateManyAsync(Arg.Any<IEnumerable<OrganizationUser>>()).Returns(
            info =>
            {
                var orgUsers = info.Arg<IEnumerable<OrganizationUser>>();
                foreach (var orgUser in orgUsers)
                {
                    orgUser.Id = Guid.NewGuid();
                }

                return Task.FromResult<ICollection<Guid>>(orgUsers.Select(u => u.Id).ToList());
            }
        );

        organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>(), Arg.Any<IEnumerable<CollectionAccessSelection>>()).Returns(
            info =>
            {
                var orgUser = info.Arg<OrganizationUser>();
                orgUser.Id = Guid.NewGuid();
                return Task.FromResult<Guid>(orgUser.Id);
            }
        );
    }

    // Must set real guids in order for dictionary of guids to not throw aggregate exceptions
    private void SetupOrgUserRepositoryCreateAsyncMock(IOrganizationUserRepository organizationUserRepository)
    {
        organizationUserRepository.CreateAsync(Arg.Any<OrganizationUser>(),
            Arg.Any<IEnumerable<CollectionAccessSelection>>()).Returns(
            info =>
            {
                var orgUser = info.Arg<OrganizationUser>();
                orgUser.Id = Guid.NewGuid();
                return Task.FromResult<Guid>(orgUser.Id);
            }
        );
    }
}
