using System.Text.Json;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Entities.Provider;
using Bit.Core.Enums;
using Bit.Core.Enums.Provider;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Core.Test.AutoFixture.PolicyFixtures;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using Organization = Bit.Core.Entities.Organization;
using OrganizationUser = Bit.Core.Entities.OrganizationUser;
using Policy = Bit.Core.Entities.Policy;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class OrganizationServiceTests
{
    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task OrgImportCreateNewUsers(SutProvider<OrganizationService> sutProvider, Guid userId,
        Organization org, List<OrganizationUserUserDetails> existingUsers, List<ImportedOrganizationUser> newUsers)
    {
        org.UseDirectory = true;
        org.Seats = 10;
        newUsers.Add(new ImportedOrganizationUser
        {
            Email = existingUsers.First().Email,
            ExternalId = existingUsers.First().ExternalId
        });
        var expectedNewUsersCount = newUsers.Count - 1;

        existingUsers.First().Type = OrganizationUserType.Owner;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id)
            .Returns(existingUsers);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetCountByOrganizationIdAsync(org.Id)
            .Returns(existingUsers.Count);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByOrganizationAsync(org.Id, OrganizationUserType.Owner)
            .Returns(existingUsers.Select(u => new OrganizationUser { Status = OrganizationUserStatusType.Confirmed, Type = OrganizationUserType.Owner, Id = u.Id }).ToList());
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(org.Id).Returns(true);

        await sutProvider.Sut.ImportAsync(org.Id, userId, null, newUsers, null, false);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == 0));
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);

        // Create new users
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .CreateManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == expectedNewUsersCount));
        await sutProvider.GetDependency<IMailService>().Received(1)
            .BulkSendOrganizationInviteEmailAsync(org.Name,
            Arg.Is<IEnumerable<(OrganizationUser, ExpiringToken)>>(messages => messages.Count() == expectedNewUsersCount), org.PlanType == PlanType.Free);

        // Send events
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventsAsync(Arg.Is<IEnumerable<(OrganizationUser, EventType, DateTime?)>>(events =>
            events.Count() == expectedNewUsersCount));
        await sutProvider.GetDependency<IReferenceEventService>().Received(1)
            .RaiseEventAsync(Arg.Is<ReferenceEvent>(referenceEvent =>
            referenceEvent.Type == ReferenceEventType.InvitedUsers && referenceEvent.Id == org.Id &&
            referenceEvent.Users == expectedNewUsersCount));
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task OrgImportCreateNewUsersAndMarryExistingUser(SutProvider<OrganizationService> sutProvider,
        Guid userId, Organization org, List<OrganizationUserUserDetails> existingUsers,
        List<ImportedOrganizationUser> newUsers)
    {
        org.UseDirectory = true;
        org.Seats = newUsers.Count + existingUsers.Count + 1;
        var reInvitedUser = existingUsers.First();
        reInvitedUser.ExternalId = null;
        newUsers.Add(new ImportedOrganizationUser
        {
            Email = reInvitedUser.Email,
            ExternalId = reInvitedUser.Email,
        });
        var expectedNewUsersCount = newUsers.Count - 1;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id)
            .Returns(existingUsers);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetCountByOrganizationIdAsync(org.Id)
            .Returns(existingUsers.Count);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(reInvitedUser.Id)
            .Returns(new OrganizationUser { Id = reInvitedUser.Id });
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByOrganizationAsync(org.Id, OrganizationUserType.Owner)
            .Returns(existingUsers.Select(u => new OrganizationUser { Status = OrganizationUserStatusType.Confirmed, Type = OrganizationUserType.Owner, Id = u.Id }).ToList());
        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        currentContext.ManageUsers(org.Id).Returns(true);

        await sutProvider.Sut.ImportAsync(org.Id, userId, null, newUsers, null, false);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default, default);

        // Upserted existing user
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == 1));

        // Created and invited new users
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .CreateManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == expectedNewUsersCount));
        await sutProvider.GetDependency<IMailService>().Received(1)
            .BulkSendOrganizationInviteEmailAsync(org.Name,
            Arg.Is<IEnumerable<(OrganizationUser, ExpiringToken)>>(messages => messages.Count() == expectedNewUsersCount), org.PlanType == PlanType.Free);

        // Sent events
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventsAsync(Arg.Is<IEnumerable<(OrganizationUser, EventType, DateTime?)>>(events =>
            events.Where(e => e.Item2 == EventType.OrganizationUser_Invited).Count() == expectedNewUsersCount));
        await sutProvider.GetDependency<IReferenceEventService>().Received(1)
            .RaiseEventAsync(Arg.Is<ReferenceEvent>(referenceEvent =>
            referenceEvent.Type == ReferenceEventType.InvitedUsers && referenceEvent.Id == org.Id &&
            referenceEvent.Users == expectedNewUsersCount));
    }

    [Theory]
    [BitAutoData(PlanType.FamiliesAnnually)]
    public async Task SignUp_PM_Family_Passes(PlanType planType, OrganizationSignup signup, SutProvider<OrganizationService> sutProvider)
    {
        signup.Plan = planType;

        var passwordManagerPlan = StaticStore.GetPasswordManagerPlan(signup.Plan);

        signup.AdditionalSeats = 0;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;

        var purchaseOrganizationPlan = StaticStore.Plans.Where(x => x.Type == signup.Plan).ToList();

        var result = await sutProvider.Sut.SignUpAsync(signup);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).CreateAsync(
            Arg.Is<Organization>(o =>
                o.Seats == passwordManagerPlan.BaseSeats + signup.AdditionalSeats
                && o.SmSeats == null
                && o.SmServiceAccounts == null));
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).CreateAsync(
            Arg.Is<OrganizationUser>(o => o.AccessSecretsManager == signup.UseSecretsManager));

        await sutProvider.GetDependency<IReferenceEventService>().Received(1)
            .RaiseEventAsync(Arg.Is<ReferenceEvent>(referenceEvent =>
                referenceEvent.Type == ReferenceEventType.Signup &&
                referenceEvent.PlanName == passwordManagerPlan.Name &&
                referenceEvent.PlanType == passwordManagerPlan.Type &&
                referenceEvent.Seats == result.Item1.Seats &&
                referenceEvent.Storage == result.Item1.MaxStorageGb));
        // TODO: add reference events for SmSeats and Service Accounts - see AC-1481

        Assert.NotNull(result);
        Assert.NotNull(result.Item1);
        Assert.NotNull(result.Item2);
        Assert.IsType<Tuple<Organization, OrganizationUser>>(result);

        await sutProvider.GetDependency<IPaymentService>().Received(1).PurchaseOrganizationAsync(
            Arg.Any<Organization>(),
            signup.PaymentMethodType.Value,
            signup.PaymentToken,
            Arg.Is<List<Plan>>(plan => plan.Single() == passwordManagerPlan),
            signup.AdditionalStorageGb,
            signup.AdditionalSeats,
            signup.PremiumAccessAddon,
            signup.TaxInfo,
            false,
            signup.AdditionalSmSeats.GetValueOrDefault(),
            signup.AdditionalServiceAccounts.GetValueOrDefault()
        );
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    public async Task SignUp_SM_Passes(PlanType planType, OrganizationSignup signup, SutProvider<OrganizationService> sutProvider)
    {
        signup.Plan = planType;

        var passwordManagerPlan = StaticStore.GetPasswordManagerPlan(signup.Plan);
        var secretsManagerPlan = StaticStore.GetSecretsManagerPlan(signup.Plan);

        signup.UseSecretsManager = true;
        signup.AdditionalSeats = 15;
        signup.AdditionalSmSeats = 10;
        signup.AdditionalServiceAccounts = 20;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;

        var purchaseOrganizationPlan = StaticStore.Plans.Where(x => x.Type == signup.Plan).ToList();

        var result = await sutProvider.Sut.SignUpAsync(signup);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).CreateAsync(
            Arg.Is<Organization>(o =>
                o.Seats == passwordManagerPlan.BaseSeats + signup.AdditionalSeats
                && o.SmSeats == secretsManagerPlan.BaseSeats + signup.AdditionalSmSeats
                && o.SmServiceAccounts == secretsManagerPlan.BaseServiceAccount + signup.AdditionalServiceAccounts));
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).CreateAsync(
            Arg.Is<OrganizationUser>(o => o.AccessSecretsManager == signup.UseSecretsManager));

        await sutProvider.GetDependency<IReferenceEventService>().Received(1)
            .RaiseEventAsync(Arg.Is<ReferenceEvent>(referenceEvent =>
                referenceEvent.Type == ReferenceEventType.Signup &&
                referenceEvent.PlanName == purchaseOrganizationPlan[0].Name &&
                referenceEvent.PlanType == purchaseOrganizationPlan[0].Type &&
                referenceEvent.Seats == result.Item1.Seats &&
                referenceEvent.Storage == result.Item1.MaxStorageGb));
        // TODO: add reference events for SmSeats and Service Accounts - see AC-1481

        Assert.NotNull(result);
        Assert.NotNull(result.Item1);
        Assert.NotNull(result.Item2);
        Assert.IsType<Tuple<Organization, OrganizationUser>>(result);

        await sutProvider.GetDependency<IPaymentService>().Received(1).PurchaseOrganizationAsync(
            Arg.Any<Organization>(),
            signup.PaymentMethodType.Value,
            signup.PaymentToken,
            Arg.Is<List<Plan>>(plan => plan.All(p => purchaseOrganizationPlan.Contains(p))),
            signup.AdditionalStorageGb,
            signup.AdditionalSeats,
            signup.PremiumAccessAddon,
            signup.TaxInfo,
            false,
            signup.AdditionalSmSeats.GetValueOrDefault(),
            signup.AdditionalServiceAccounts.GetValueOrDefault()
        );
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_SecretManager_AdditionalServiceAccounts_NotAllowedByPlan_ShouldThrowException(OrganizationSignup signup, SutProvider<OrganizationService> sutProvider)
    {
        signup.AdditionalSmSeats = 0;
        signup.AdditionalSeats = 0;
        signup.Plan = PlanType.Free;
        signup.UseSecretsManager = true;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.AdditionalServiceAccounts = 10;
        signup.AdditionalStorageGb = 0;

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpAsync(signup));
        Assert.Contains("Plan does not allow additional Service Accounts.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_SMSeatsGreatThanPMSeat_ShouldThrowException(OrganizationSignup signup, SutProvider<OrganizationService> sutProvider)
    {
        signup.AdditionalSmSeats = 100;
        signup.AdditionalSeats = 10;
        signup.Plan = PlanType.EnterpriseAnnually;
        signup.UseSecretsManager = true;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.AdditionalServiceAccounts = 10;

        var exception = await Assert.ThrowsAsync<BadRequestException>(
           () => sutProvider.Sut.SignUpAsync(signup));
        Assert.Contains("You cannot have more Secrets Manager seats than Password Manager seats", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpAsync_InvalidateServiceAccount_ShouldThrowException(OrganizationSignup signup, SutProvider<OrganizationService> sutProvider)
    {
        signup.AdditionalSmSeats = 10;
        signup.AdditionalSeats = 10;
        signup.Plan = PlanType.EnterpriseAnnually;
        signup.UseSecretsManager = true;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.AdditionalServiceAccounts = -10;

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpAsync(signup));
        Assert.Contains("You can't subtract Service Accounts!", exception.Message);
    }

    [Theory]
    [OrganizationInviteCustomize(InviteeUserType = OrganizationUserType.User,
         InvitorUserType = OrganizationUserType.Owner), BitAutoData]
    public async Task InviteUser_NoEmails_Throws(Organization organization, OrganizationUser invitor,
        OrganizationUserInvite invite, SutProvider<OrganizationService> sutProvider)
    {
        invite.Emails = null;
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) }));
    }

    [Theory]
    [OrganizationInviteCustomize, BitAutoData]
    public async Task InviteUser_DuplicateEmails_PassesWithoutDuplicates(Organization organization, OrganizationUser invitor,
                [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        OrganizationUserInvite invite, SutProvider<OrganizationService> sutProvider)
    {
        invite.Emails = invite.Emails.Append(invite.Emails.First());

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        organizationUserRepository.GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new[] { owner });

        await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) });

        await sutProvider.GetDependency<IMailService>().Received(1)
            .BulkSendOrganizationInviteEmailAsync(organization.Name,
                Arg.Is<IEnumerable<(OrganizationUser, ExpiringToken)>>(v => v.Count() == invite.Emails.Distinct().Count()), organization.PlanType == PlanType.Free);
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.Admin,
        InvitorUserType = OrganizationUserType.Owner
    ), BitAutoData]
    public async Task InviteUser_NoOwner_Throws(Organization organization, OrganizationUser invitor,
        OrganizationUserInvite invite, SutProvider<OrganizationService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) }));
        Assert.Contains("Organization must have at least one confirmed owner.", exception.Message);
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.Owner,
        InvitorUserType = OrganizationUserType.Admin
    ), BitAutoData]
    public async Task InviteUser_NonOwnerConfiguringOwner_Throws(Organization organization, OrganizationUserInvite invite,
        OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
    {
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        currentContext.OrganizationAdmin(organization.Id).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) }));
        Assert.Contains("only an owner", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.Custom,
        InvitorUserType = OrganizationUserType.User
    ), BitAutoData]
    public async Task InviteUser_NonAdminConfiguringAdmin_Throws(Organization organization, OrganizationUserInvite invite,
        OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
    {
        organization.UseCustomPermissions = true;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        currentContext.OrganizationUser(organization.Id).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) }));
        Assert.Contains("your account does not have permission to manage users", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [OrganizationInviteCustomize(
         InviteeUserType = OrganizationUserType.Custom,
         InvitorUserType = OrganizationUserType.Admin
     ), BitAutoData]
    public async Task InviteUser_WithCustomType_WhenUseCustomPermissionsIsFalse_Throws(Organization organization, OrganizationUserInvite invite,
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
            () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) }));
        Assert.Contains("to enable custom permissions", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [OrganizationInviteCustomize(
         InviteeUserType = OrganizationUserType.Custom,
         InvitorUserType = OrganizationUserType.Admin
     ), BitAutoData]
    public async Task InviteUser_WithCustomType_WhenUseCustomPermissionsIsTrue_Passes(Organization organization, OrganizationUserInvite invite,
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
        organizationUserRepository.GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new[] { invitor });
        currentContext.OrganizationOwner(organization.Id).Returns(true);
        currentContext.ManageUsers(organization.Id).Returns(true);

        await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) });
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Manager)]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.User)]
    public async Task InviteUser_WithNonCustomType_WhenUseCustomPermissionsIsFalse_Passes(OrganizationUserType inviteUserType, Organization organization, OrganizationUserInvite invite,
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

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        organizationUserRepository.GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new[] { invitor });
        currentContext.OrganizationOwner(organization.Id).Returns(true);
        currentContext.ManageUsers(organization.Id).Returns(true);

        await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) });
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.Manager,
        InvitorUserType = OrganizationUserType.Custom
    ), BitAutoData]
    public async Task InviteUser_CustomUserWithoutManageUsersConfiguringUser_Throws(Organization organization, OrganizationUserInvite invite,
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
        currentContext.OrganizationCustom(organization.Id).Returns(true);
        currentContext.ManageUsers(organization.Id).Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) }));
        Assert.Contains("account does not have permission", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.Admin,
        InvitorUserType = OrganizationUserType.Custom
    ), BitAutoData]
    public async Task InviteUser_CustomUserConfiguringAdmin_Throws(Organization organization, OrganizationUserInvite invite,
        OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
    {
        invitor.Permissions = JsonSerializer.Serialize(new Permissions() { ManageUsers = true },
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        currentContext.OrganizationCustom(organization.Id).Returns(true);
        currentContext.ManageUsers(organization.Id).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) }));
        Assert.Contains("can not manage admins", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.User,
        InvitorUserType = OrganizationUserType.Owner
    ), BitAutoData]
    public async Task InviteUser_NoPermissionsObject_Passes(Organization organization, OrganizationUserInvite invite,
        OrganizationUser invitor, SutProvider<OrganizationService> sutProvider)
    {
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

        await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, new (OrganizationUserInvite, string)[] { (invite, null) });
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.User,
        InvitorUserType = OrganizationUserType.Custom
    ), BitAutoData]
    public async Task InviteUser_Passes(Organization organization, IEnumerable<(OrganizationUserInvite invite, string externalId)> invites,
        OrganizationUser invitor,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        SutProvider<OrganizationService> sutProvider)
    {
        invitor.Permissions = JsonSerializer.Serialize(new Permissions() { ManageUsers = true },
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        organizationUserRepository.GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new[] { owner });
        currentContext.ManageUsers(organization.Id).Returns(true);
        currentContext.AccessReports(organization.Id).Returns(true);
        currentContext.ManageGroups(organization.Id).Returns(true);
        currentContext.ManagePolicies(organization.Id).Returns(true);
        currentContext.ManageScim(organization.Id).Returns(true);
        currentContext.ManageSso(organization.Id).Returns(true);
        currentContext.AccessEventLogs(organization.Id).Returns(true);
        currentContext.AccessImportExport(organization.Id).Returns(true);
        currentContext.DeleteAssignedCollections(organization.Id).Returns(true);
        currentContext.EditAnyCollection(organization.Id).Returns(true);
        currentContext.EditAssignedCollections(organization.Id).Returns(true);
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

        await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, invites);

        await sutProvider.GetDependency<IMailService>().Received(1)
            .BulkSendOrganizationInviteEmailAsync(organization.Name,
                Arg.Is<IEnumerable<(OrganizationUser, ExpiringToken)>>(v => v.Count() == invites.SelectMany(i => i.invite.Emails).Count()), organization.PlanType == PlanType.Free);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, DateTime?)>>());
    }

    [Theory]
    [OrganizationInviteCustomize(
        InviteeUserType = OrganizationUserType.User,
        InvitorUserType = OrganizationUserType.Custom
    ), BitAutoData]
    public async Task InviteUser_WithEventSystemUser_Passes(Organization organization, EventSystemUser eventSystemUser, IEnumerable<(OrganizationUserInvite invite, string externalId)> invites,
        OrganizationUser invitor,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        SutProvider<OrganizationService> sutProvider)
    {
        invitor.Permissions = JsonSerializer.Serialize(new Permissions() { ManageUsers = true },
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        organizationUserRepository.GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new[] { owner });
        currentContext.ManageUsers(organization.Id).Returns(true);

        await sutProvider.Sut.InviteUsersAsync(organization.Id, eventSystemUser, invites);

        await sutProvider.GetDependency<IMailService>().Received(1)
            .BulkSendOrganizationInviteEmailAsync(organization.Name,
                Arg.Is<IEnumerable<(OrganizationUser, ExpiringToken)>>(v => v.Count() == invites.SelectMany(i => i.invite.Emails).Count()), organization.PlanType == PlanType.Free);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, EventSystemUser, DateTime?)>>());
    }

    [Theory, BitAutoData]
    public async Task InviteUser_WithSecretsManager_Passes(Organization organization,
        IEnumerable<(OrganizationUserInvite invite, string externalId)> invites,
        [OrganizationUser(type: OrganizationUserType.Owner, status: OrganizationUserStatusType.Confirmed)] OrganizationUser savingUser,
        SutProvider<OrganizationService> sutProvider)
    {
        organization.PlanType = PlanType.EnterpriseAnnually;
        InviteUserHelper_ArrangeValidPermissions(organization, savingUser, sutProvider);

        // Set up some invites to grant access to SM
        invites.First().invite.AccessSecretsManager = true;
        var invitedSmUsers = invites.First().invite.Emails.Count();

        // Assume we need to add seats for all invited SM users
        sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>()
            .CountNewSmSeatsRequiredAsync(organization.Id, invitedSmUsers).Returns(invitedSmUsers);

        await sutProvider.Sut.InviteUsersAsync(organization.Id, savingUser.Id, invites);

        sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>().Received(1)
            .UpdateSubscriptionAsync(Arg.Is<SecretsManagerSubscriptionUpdate>(update =>
                update.SmSeats == organization.SmSeats + invitedSmUsers &&
                !update.SmServiceAccountsChanged &&
                !update.MaxAutoscaleSmSeatsChanged &&
                !update.MaxAutoscaleSmSeatsChanged));
    }

    [Theory, BitAutoData]
    public async Task InviteUser_WithSecretsManager_WhenErrorIsThrown_RevertsAutoscaling(Organization organization,
        IEnumerable<(OrganizationUserInvite invite, string externalId)> invites,
        [OrganizationUser(type: OrganizationUserType.Owner, status: OrganizationUserStatusType.Confirmed)] OrganizationUser savingUser,
        SutProvider<OrganizationService> sutProvider)
    {
        var initialSmSeats = organization.SmSeats;
        InviteUserHelper_ArrangeValidPermissions(organization, savingUser, sutProvider);

        // Set up some invites to grant access to SM
        invites.First().invite.AccessSecretsManager = true;
        var invitedSmUsers = invites.First().invite.Emails.Count();

        // Assume we need to add seats for all invited SM users
        sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>()
            .CountNewSmSeatsRequiredAsync(organization.Id, invitedSmUsers).Returns(invitedSmUsers);

        // Mock SecretsManagerSubscriptionUpdateCommand to actually change the organization's subscription in memory
        sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .UpdateSubscriptionAsync(Arg.Any<SecretsManagerSubscriptionUpdate>())
            .ReturnsForAnyArgs(Task.FromResult(0)).AndDoes(x => organization.SmSeats += invitedSmUsers);

        // Throw error at the end of the try block
        sutProvider.GetDependency<IReferenceEventService>().RaiseEventAsync(default).ThrowsForAnyArgs<BadRequestException>();

        await Assert.ThrowsAsync<AggregateException>(async () => await sutProvider.Sut.InviteUsersAsync(organization.Id, savingUser.Id, invites));

        // OrgUser is reverted
        // Note: we don't know what their guids are so comparing length is the best we can do
        var invitedEmails = invites.SelectMany(i => i.invite.Emails);
        sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).DeleteManyAsync(
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
        savingUser.OrganizationId = organization.Id;
        organization.UseCustomPermissions = true;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByOrganizationAsync(savingUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { savingUser });
    }

    [Theory, BitAutoData]
    public async Task SaveUser_NoUserId_Throws(OrganizationUser user, Guid? savingUserId,
        IEnumerable<CollectionAccessSelection> collections, IEnumerable<Guid> groups, SutProvider<OrganizationService> sutProvider)
    {
        user.Id = default(Guid);
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveUserAsync(user, savingUserId, collections, groups));
        Assert.Contains("invite the user first", exception.Message.ToLowerInvariant());
    }

    [Theory, BitAutoData]
    public async Task SaveUser_NoChangeToData_Throws(OrganizationUser user, Guid? savingUserId,
        IEnumerable<CollectionAccessSelection> collections, IEnumerable<Guid> groups, SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        organizationUserRepository.GetByIdAsync(user.Id).Returns(user);
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveUserAsync(user, savingUserId, collections, groups));
        Assert.Contains("make changes before saving", exception.Message.ToLowerInvariant());
    }

    [Theory, BitAutoData]
    public async Task SaveUser_Passes(
        Organization organization,
        OrganizationUser oldUserData,
        OrganizationUser newUserData,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        Permissions permissions,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser savingUser,
        SutProvider<OrganizationService> sutProvider)
    {
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        newUserData.Id = oldUserData.Id;
        newUserData.UserId = oldUserData.UserId;
        newUserData.OrganizationId = savingUser.OrganizationId = oldUserData.OrganizationId = organization.Id;
        newUserData.Permissions = JsonSerializer.Serialize(permissions, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);
        organizationUserRepository.GetManyByOrganizationAsync(savingUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { savingUser });
        currentContext.OrganizationOwner(savingUser.OrganizationId).Returns(true);

        await sutProvider.Sut.SaveUserAsync(newUserData, savingUser.UserId, collections, groups);
    }

    [Theory, BitAutoData]
    public async Task SaveUser_WithCustomType_WhenUseCustomPermissionsIsFalse_Throws(
        Organization organization,
        OrganizationUser oldUserData,
        [OrganizationUser(type: OrganizationUserType.Custom)] OrganizationUser newUserData,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser savingUser,
        SutProvider<OrganizationService> sutProvider)
    {
        organization.UseCustomPermissions = false;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        newUserData.Id = oldUserData.Id;
        newUserData.UserId = oldUserData.UserId;
        newUserData.OrganizationId = savingUser.OrganizationId = oldUserData.OrganizationId = organization.Id;
        newUserData.Permissions = null;
        organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);
        organizationUserRepository.GetManyByOrganizationAsync(savingUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { savingUser });
        currentContext.OrganizationOwner(savingUser.OrganizationId).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveUserAsync(newUserData, savingUser.UserId, collections, groups));
        Assert.Contains("to enable custom permissions", exception.Message.ToLowerInvariant());
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Manager)]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.User)]
    public async Task SaveUser_WithNonCustomType_WhenUseCustomPermissionsIsFalse_Passes(
        OrganizationUserType newUserType,
        Organization organization,
        OrganizationUser oldUserData,
        OrganizationUser newUserData,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        Permissions permissions,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser savingUser,
        SutProvider<OrganizationService> sutProvider)
    {
        organization.UseCustomPermissions = false;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        newUserData.Id = oldUserData.Id;
        newUserData.UserId = oldUserData.UserId;
        newUserData.OrganizationId = savingUser.OrganizationId = oldUserData.OrganizationId = organization.Id;
        newUserData.Type = newUserType;
        newUserData.Permissions = JsonSerializer.Serialize(permissions, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);
        organizationUserRepository.GetManyByOrganizationAsync(savingUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { savingUser });
        currentContext.OrganizationOwner(savingUser.OrganizationId).Returns(true);

        await sutProvider.Sut.SaveUserAsync(newUserData, savingUser.UserId, collections, groups);
    }

    [Theory, BitAutoData]
    public async Task SaveUser_WithCustomType_WhenUseCustomPermissionsIsTrue_Passes(
        Organization organization,
        OrganizationUser oldUserData,
        [OrganizationUser(type: OrganizationUserType.Custom)] OrganizationUser newUserData,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        Permissions permissions,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser savingUser,
        SutProvider<OrganizationService> sutProvider)
    {
        organization.UseCustomPermissions = true;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        newUserData.Id = oldUserData.Id;
        newUserData.UserId = oldUserData.UserId;
        newUserData.OrganizationId = savingUser.OrganizationId = oldUserData.OrganizationId = organization.Id;
        newUserData.Permissions = JsonSerializer.Serialize(permissions, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);
        organizationUserRepository.GetManyByOrganizationAsync(savingUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { savingUser });
        currentContext.OrganizationOwner(savingUser.OrganizationId).Returns(true);

        await sutProvider.Sut.SaveUserAsync(newUserData, savingUser.UserId, collections, groups);
    }

    [Theory, BitAutoData]
    public async Task SaveUser_WithCustomPermission_WhenSavingUserHasCustomPermission_Passes(
        Organization organization,
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser oldUserData,
        [OrganizationUser(type: OrganizationUserType.Custom)] OrganizationUser newUserData,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        [OrganizationUser(type: OrganizationUserType.Custom)] OrganizationUser savingUser,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser organizationOwner,
        SutProvider<OrganizationService> sutProvider)
    {
        organization.UseCustomPermissions = true;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        newUserData.Id = oldUserData.Id;
        newUserData.UserId = oldUserData.UserId;
        newUserData.OrganizationId = savingUser.OrganizationId = oldUserData.OrganizationId = organizationOwner.OrganizationId = organization.Id;
        newUserData.Permissions = JsonSerializer.Serialize(new Permissions { AccessReports = true }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);
        organizationUserRepository.GetManyByOrganizationAsync(savingUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { organizationOwner });
        currentContext.OrganizationCustom(savingUser.OrganizationId).Returns(true);
        currentContext.ManageUsers(savingUser.OrganizationId).Returns(true);
        currentContext.AccessReports(savingUser.OrganizationId).Returns(true);

        await sutProvider.Sut.SaveUserAsync(newUserData, savingUser.UserId, collections, groups);
    }

    [Theory, BitAutoData]
    public async Task SaveUser_WithCustomPermission_WhenSavingUserDoesNotHaveCustomPermission_Throws(
        Organization organization,
        [OrganizationUser(type: OrganizationUserType.User)] OrganizationUser oldUserData,
        [OrganizationUser(type: OrganizationUserType.Custom)] OrganizationUser newUserData,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        [OrganizationUser(type: OrganizationUserType.Custom)] OrganizationUser savingUser,
        SutProvider<OrganizationService> sutProvider)
    {
        organization.UseCustomPermissions = true;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        newUserData.Id = oldUserData.Id;
        newUserData.UserId = oldUserData.UserId;
        newUserData.OrganizationId = savingUser.OrganizationId = oldUserData.OrganizationId = organization.Id;
        newUserData.Permissions = JsonSerializer.Serialize(new Permissions { AccessReports = true }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);
        currentContext.OrganizationCustom(savingUser.OrganizationId).Returns(true);
        currentContext.ManageUsers(savingUser.OrganizationId).Returns(true);
        currentContext.AccessReports(savingUser.OrganizationId).Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveUserAsync(newUserData, savingUser.UserId, collections, groups));
        Assert.Contains("custom users can only grant the same custom permissions that they have", exception.Message.ToLowerInvariant());
    }

    [Theory, BitAutoData]
    public async Task SaveUser_WithCustomPermission_WhenUpgradingToAdmin_Throws(
        Organization organization,
        [OrganizationUser(type: OrganizationUserType.Custom)] OrganizationUser oldUserData,
        [OrganizationUser(type: OrganizationUserType.Admin)] OrganizationUser newUserData,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups,
        SutProvider<OrganizationService> sutProvider)
    {
        organization.UseCustomPermissions = true;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        newUserData.Id = oldUserData.Id;
        newUserData.UserId = oldUserData.UserId;
        newUserData.OrganizationId = oldUserData.OrganizationId = organization.Id;
        newUserData.Permissions = JsonSerializer.Serialize(new Permissions { AccessReports = true }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        organizationUserRepository.GetByIdAsync(oldUserData.Id).Returns(oldUserData);
        currentContext.OrganizationCustom(oldUserData.OrganizationId).Returns(true);
        currentContext.ManageUsers(oldUserData.OrganizationId).Returns(true);
        currentContext.AccessReports(oldUserData.OrganizationId).Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveUserAsync(newUserData, oldUserData.UserId, collections, groups));
        Assert.Contains("custom users can not manage admins or owners", exception.Message.ToLowerInvariant());
    }

    [Theory, BitAutoData]
    public async Task DeleteUser_InvalidUser(OrganizationUser organizationUser, OrganizationUser deletingUser,
        SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        organizationUserRepository.GetByIdAsync(organizationUser.Id).Returns(organizationUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteUserAsync(Guid.NewGuid(), organizationUser.Id, deletingUser.UserId));
        Assert.Contains("User not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task DeleteUser_RemoveYourself(OrganizationUser deletingUser, SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        organizationUserRepository.GetByIdAsync(deletingUser.Id).Returns(deletingUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteUserAsync(deletingUser.OrganizationId, deletingUser.Id, deletingUser.UserId));
        Assert.Contains("You cannot remove yourself.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task DeleteUser_NonOwnerRemoveOwner(
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser organizationUser,
        [OrganizationUser(type: OrganizationUserType.Admin)] OrganizationUser deletingUser,
        SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationUser.OrganizationId = deletingUser.OrganizationId;
        organizationUserRepository.GetByIdAsync(organizationUser.Id).Returns(organizationUser);
        currentContext.OrganizationAdmin(deletingUser.OrganizationId).Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteUserAsync(deletingUser.OrganizationId, organizationUser.Id, deletingUser.UserId));
        Assert.Contains("Only owners can delete other owners.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task DeleteUser_LastOwner(
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser organizationUser,
        OrganizationUser deletingUser,
        SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        organizationUser.OrganizationId = deletingUser.OrganizationId;
        organizationUserRepository.GetByIdAsync(organizationUser.Id).Returns(organizationUser);
        organizationUserRepository.GetManyByOrganizationAsync(deletingUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(new[] { organizationUser });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteUserAsync(deletingUser.OrganizationId, organizationUser.Id, null));
        Assert.Contains("Organization must have at least one confirmed owner.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task DeleteUser_Success(
        OrganizationUser organizationUser,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser deletingUser,
        SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationUser.OrganizationId = deletingUser.OrganizationId;
        organizationUserRepository.GetByIdAsync(organizationUser.Id).Returns(organizationUser);
        organizationUserRepository.GetByIdAsync(deletingUser.Id).Returns(deletingUser);
        organizationUserRepository.GetManyByOrganizationAsync(deletingUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(new[] { deletingUser, organizationUser });
        currentContext.OrganizationOwner(deletingUser.OrganizationId).Returns(true);

        await sutProvider.Sut.DeleteUserAsync(deletingUser.OrganizationId, organizationUser.Id, deletingUser.UserId);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed);
    }

    [Theory, BitAutoData]
    public async Task DeleteUser_WithEventSystemUser_Success(
        OrganizationUser organizationUser,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser deletingUser, EventSystemUser eventSystemUser,
        SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        organizationUser.OrganizationId = deletingUser.OrganizationId;
        organizationUserRepository.GetByIdAsync(organizationUser.Id).Returns(organizationUser);
        organizationUserRepository.GetByIdAsync(deletingUser.Id).Returns(deletingUser);
        organizationUserRepository.GetManyByOrganizationAsync(deletingUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(new[] { deletingUser, organizationUser });
        currentContext.OrganizationOwner(deletingUser.OrganizationId).Returns(true);

        await sutProvider.Sut.DeleteUserAsync(deletingUser.OrganizationId, organizationUser.Id, eventSystemUser);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Removed, eventSystemUser);
    }

    [Theory, BitAutoData]
    public async Task DeleteUsers_FilterInvalid(OrganizationUser organizationUser, OrganizationUser deletingUser,
        SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationUsers = new[] { organizationUser };
        var organizationUserIds = organizationUsers.Select(u => u.Id);
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId));
        Assert.Contains("Users invalid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task DeleteUsers_RemoveYourself(
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUser,
        OrganizationUser deletingUser,
        SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationUsers = new[] { deletingUser };
        var organizationUserIds = organizationUsers.Select(u => u.Id);
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);
        organizationUserRepository.GetManyByOrganizationAsync(default, default).ReturnsForAnyArgs(new[] { orgUser });

        var result = await sutProvider.Sut.DeleteUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId);
        Assert.Contains("You cannot remove yourself.", result[0].Item2);
    }

    [Theory, BitAutoData]
    public async Task DeleteUsers_NonOwnerRemoveOwner(
        [OrganizationUser(type: OrganizationUserType.Admin)] OrganizationUser deletingUser,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Confirmed)] OrganizationUser orgUser2,
        SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        orgUser1.OrganizationId = orgUser2.OrganizationId = deletingUser.OrganizationId;
        var organizationUsers = new[] { orgUser1 };
        var organizationUserIds = organizationUsers.Select(u => u.Id);
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);
        organizationUserRepository.GetManyByOrganizationAsync(default, default).ReturnsForAnyArgs(new[] { orgUser2 });

        var result = await sutProvider.Sut.DeleteUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId);
        Assert.Contains("Only owners can delete other owners.", result[0].Item2);
    }

    [Theory, BitAutoData]
    public async Task DeleteUsers_LastOwner(
        [OrganizationUser(status: OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUser,
        SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        var organizationUsers = new[] { orgUser };
        var organizationUserIds = organizationUsers.Select(u => u.Id);
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);
        organizationUserRepository.GetManyByOrganizationAsync(orgUser.OrganizationId, OrganizationUserType.Owner).Returns(organizationUsers);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteUsersAsync(orgUser.OrganizationId, organizationUserIds, null));
        Assert.Contains("Organization must have at least one confirmed owner.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task DeleteUsers_Success(
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser deletingUser,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser orgUser1, OrganizationUser orgUser2,
        SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var currentContext = sutProvider.GetDependency<ICurrentContext>();

        orgUser1.OrganizationId = orgUser2.OrganizationId = deletingUser.OrganizationId;
        var organizationUsers = new[] { orgUser1, orgUser2 };
        var organizationUserIds = organizationUsers.Select(u => u.Id);
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(organizationUsers);
        organizationUserRepository.GetByIdAsync(deletingUser.Id).Returns(deletingUser);
        organizationUserRepository.GetManyByOrganizationAsync(deletingUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(new[] { deletingUser, orgUser1 });
        currentContext.OrganizationOwner(deletingUser.OrganizationId).Returns(true);

        await sutProvider.Sut.DeleteUsersAsync(deletingUser.OrganizationId, organizationUserIds, deletingUser.UserId);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUser_InvalidStatus(OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Invited)] OrganizationUser orgUser, string key,
        SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var userService = Substitute.For<IUserService>();

        organizationUserRepository.GetByIdAsync(orgUser.Id).Returns(orgUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, userService));
        Assert.Contains("User not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUser_WrongOrganization(OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, string key,
        SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var userService = Substitute.For<IUserService>();

        organizationUserRepository.GetByIdAsync(orgUser.Id).Returns(orgUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(confirmingUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, userService));
        Assert.Contains("User not valid.", exception.Message);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task ConfirmUserToFree_AlreadyFreeAdminOrOwner_Throws(OrganizationUserType userType, Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        string key, SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userService = Substitute.For<IUserService>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();

        org.PlanType = PlanType.Free;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;
        orgUser.Type = userType;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(orgUser.UserId.Value).Returns(1);
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, userService));
        Assert.Contains("User can only be an admin of one free organization.", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.Custom, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.Custom, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.EnterpriseAnnually, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.EnterpriseAnnually, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.EnterpriseAnnually2019, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.EnterpriseAnnually2019, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.EnterpriseMonthly, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.EnterpriseMonthly, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.EnterpriseMonthly2019, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.EnterpriseMonthly2019, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.FamiliesAnnually, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.FamiliesAnnually, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.FamiliesAnnually2019, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.FamiliesAnnually2019, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.TeamsAnnually, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.TeamsAnnually, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.TeamsAnnually2019, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.TeamsAnnually2019, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.TeamsMonthly, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.TeamsMonthly, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.TeamsMonthly2019, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.TeamsMonthly2019, OrganizationUserType.Owner)]
    public async Task ConfirmUserToNonFree_AlreadyFreeAdminOrOwner_DoesNotThrow(PlanType planType, OrganizationUserType orgUserType, Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        string key, SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userService = Substitute.For<IUserService>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();

        org.PlanType = planType;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;
        orgUser.Type = orgUserType;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(orgUser.UserId.Value).Returns(1);
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, userService);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
        await sutProvider.GetDependency<IMailService>().Received(1).SendOrganizationConfirmedEmailAsync(org.Name, user.Email);
        await organizationUserRepository.Received(1).ReplaceManyAsync(Arg.Is<List<OrganizationUser>>(users => users.Contains(orgUser) && users.Count == 1));
    }


    [Theory, BitAutoData]
    public async Task ConfirmUser_SingleOrgPolicy(Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        OrganizationUser orgUserAnotherOrg, [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        string key, SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var policyRepository = sutProvider.GetDependency<IPolicyRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var userService = Substitute.For<IUserService>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.Status = OrganizationUserStatusType.Accepted;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] { orgUserAnotherOrg });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });
        policyRepository.GetManyByOrganizationIdAsync(org.Id).Returns(new[] { singleOrgPolicy });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, userService));
        Assert.Contains("User is a member of another organization.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUser_TwoFactorPolicy(Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        OrganizationUser orgUserAnotherOrg, [Policy(PolicyType.TwoFactorAuthentication)] Policy twoFactorPolicy,
        string key, SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var policyRepository = sutProvider.GetDependency<IPolicyRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var userService = Substitute.For<IUserService>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] { orgUserAnotherOrg });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });
        policyRepository.GetManyByOrganizationIdAsync(org.Id).Returns(new[] { twoFactorPolicy });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, userService));
        Assert.Contains("User does not have two-step login enabled.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUser_Success(Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        [Policy(PolicyType.TwoFactorAuthentication)] Policy twoFactorPolicy,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy, string key, SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var policyRepository = sutProvider.GetDependency<IPolicyRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var userService = Substitute.For<IUserService>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });
        policyRepository.GetManyByOrganizationIdAsync(org.Id).Returns(new[] { twoFactorPolicy, singleOrgPolicy });
        userService.TwoFactorIsEnabledAsync(user).Returns(true);

        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, userService);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUsers_Success(Organization org,
        OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser2,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser3,
        OrganizationUser anotherOrgUser, User user1, User user2, User user3,
        [Policy(PolicyType.TwoFactorAuthentication)] Policy twoFactorPolicy,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy, string key, SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var policyRepository = sutProvider.GetDependency<IPolicyRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var userService = Substitute.For<IUserService>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser1.OrganizationId = orgUser2.OrganizationId = orgUser3.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser1.UserId = user1.Id;
        orgUser2.UserId = user2.Id;
        orgUser3.UserId = user3.Id;
        anotherOrgUser.UserId = user3.Id;
        var orgUsers = new[] { orgUser1, orgUser2, orgUser3 };
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(orgUsers);
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user1, user2, user3 });
        policyRepository.GetManyByOrganizationIdAsync(org.Id).Returns(new[] { twoFactorPolicy, singleOrgPolicy });
        userService.TwoFactorIsEnabledAsync(user1).Returns(true);
        userService.TwoFactorIsEnabledAsync(user2).Returns(false);
        userService.TwoFactorIsEnabledAsync(user3).Returns(true);
        organizationUserRepository.GetManyByManyUsersAsync(default)
            .ReturnsForAnyArgs(new[] { orgUser1, orgUser2, orgUser3, anotherOrgUser });

        var keys = orgUsers.ToDictionary(ou => ou.Id, _ => key);
        var result = await sutProvider.Sut.ConfirmUsersAsync(confirmingUser.OrganizationId, keys, confirmingUser.Id, userService);
        Assert.Contains("", result[0].Item2);
        Assert.Contains("User does not have two-step login enabled.", result[1].Item2);
        Assert.Contains("User is a member of another organization.", result[2].Item2);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationKeysAsync_WithoutManageResetPassword_Throws(Guid orgId, string publicKey,
        string privateKey, SutProvider<OrganizationService> sutProvider)
    {
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.ManageResetPassword(orgId).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sutProvider.Sut.UpdateOrganizationKeysAsync(orgId, publicKey, privateKey));
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationKeysAsync_KeysAlreadySet_Throws(Organization org, string publicKey,
        string privateKey, SutProvider<OrganizationService> sutProvider)
    {
        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        currentContext.ManageResetPassword(org.Id).Returns(true);

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(org.Id).Returns(org);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateOrganizationKeysAsync(org.Id, publicKey, privateKey));
        Assert.Contains("Organization Keys already exist", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationKeysAsync_KeysAlreadySet_Success(Organization org, string publicKey,
        string privateKey, SutProvider<OrganizationService> sutProvider)
    {
        org.PublicKey = null;
        org.PrivateKey = null;

        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        currentContext.ManageResetPassword(org.Id).Returns(true);

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(org.Id).Returns(org);

        await sutProvider.Sut.UpdateOrganizationKeysAsync(org.Id, publicKey, privateKey);
    }

    [Theory]
    [PaidOrganizationCustomize(CheckedPlanType = PlanType.EnterpriseAnnually)]
    [BitAutoData("Cannot set max seat autoscaling below seat count", 1, 0, 2)]
    [BitAutoData("Cannot set max seat autoscaling below seat count", 4, -1, 6)]
    public async Task Enterprise_UpdateSubscription_BadInputThrows(string expectedMessage,
        int? maxAutoscaleSeats, int seatAdjustment, int? currentSeats, Organization organization, SutProvider<OrganizationService> sutProvider)
        => await UpdateSubscription_BadInputThrows(expectedMessage, maxAutoscaleSeats, seatAdjustment, currentSeats, organization, sutProvider);
    [Theory]
    [FreeOrganizationCustomize]
    [BitAutoData("Your plan does not allow seat autoscaling", 10, 0, null)]
    public async Task Free_UpdateSubscription_BadInputThrows(string expectedMessage,
        int? maxAutoscaleSeats, int seatAdjustment, int? currentSeats, Organization organization, SutProvider<OrganizationService> sutProvider)
        => await UpdateSubscription_BadInputThrows(expectedMessage, maxAutoscaleSeats, seatAdjustment, currentSeats, organization, sutProvider);

    private async Task UpdateSubscription_BadInputThrows(string expectedMessage,
        int? maxAutoscaleSeats, int seatAdjustment, int? currentSeats, Organization organization, SutProvider<OrganizationService> sutProvider)
    {
        organization.Seats = currentSeats;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

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

    [Theory, PaidOrganizationCustomize]
    [BitAutoData(0, 100, null, true, "")]
    [BitAutoData(0, 100, 100, true, "")]
    [BitAutoData(0, null, 100, true, "")]
    [BitAutoData(1, 100, null, true, "")]
    [BitAutoData(1, 100, 100, false, "Seat limit has been reached")]
    public void CanScale(int seatsToAdd, int? currentSeats, int? maxAutoscaleSeats,
        bool expectedResult, string expectedFailureMessage, Organization organization,
        SutProvider<OrganizationService> sutProvider)
    {
        organization.Seats = currentSeats;
        organization.MaxAutoscaleSeats = maxAutoscaleSeats;
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);

        var (result, failureMessage) = sutProvider.Sut.CanScale(organization, seatsToAdd);

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
    public void CanScale_FailsOnSelfHosted(Organization organization,
        SutProvider<OrganizationService> sutProvider)
    {
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(true);
        var (result, failureMessage) = sutProvider.Sut.CanScale(organization, 10);

        Assert.False(result);
        Assert.Contains("Cannot autoscale on self-hosted instance", failureMessage);
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task Delete_Success(Organization organization, SutProvider<OrganizationService> sutProvider)
    {
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var applicationCacheService = sutProvider.GetDependency<IApplicationCacheService>();

        await sutProvider.Sut.DeleteAsync(organization);

        await organizationRepository.Received().DeleteAsync(organization);
        await applicationCacheService.Received().DeleteOrganizationAbilityAsync(organization.Id);
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task Delete_Fails_KeyConnector(Organization organization, SutProvider<OrganizationService> sutProvider,
        SsoConfig ssoConfig)
    {
        ssoConfig.Enabled = true;
        ssoConfig.SetData(new SsoConfigurationData { MemberDecryptionType = MemberDecryptionType.KeyConnector });
        var ssoConfigRepository = sutProvider.GetDependency<ISsoConfigRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var applicationCacheService = sutProvider.GetDependency<IApplicationCacheService>();

        ssoConfigRepository.GetByOrganizationIdAsync(organization.Id).Returns(ssoConfig);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteAsync(organization));

        Assert.Contains("You cannot delete an Organization that is using Key Connector.", exception.Message);

        await organizationRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default);
        await applicationCacheService.DidNotReceiveWithAnyArgs().DeleteOrganizationAbilityAsync(default);
    }

    private void RestoreRevokeUser_Setup(Organization organization, OrganizationUser owner, OrganizationUser organizationUser, SutProvider<OrganizationService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationUser.OrganizationId).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        organizationUserRepository.GetManyByOrganizationAsync(organizationUser.OrganizationId, OrganizationUserType.Owner)
            .Returns(new[] { owner });
    }

    [Theory, BitAutoData]
    public async Task RevokeUser_Success(Organization organization, [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser] OrganizationUser organizationUser, SutProvider<OrganizationService> sutProvider)
    {
        RestoreRevokeUser_Setup(organization, owner, organizationUser, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();

        await sutProvider.Sut.RevokeUserAsync(organizationUser, owner.Id);

        await organizationUserRepository.Received().RevokeAsync(organizationUser.Id);
        await eventService.Received()
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Revoked);
    }

    [Theory, BitAutoData]
    public async Task RevokeUser_WithEventSystemUser_Success(Organization organization, [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser] OrganizationUser organizationUser, EventSystemUser eventSystemUser, SutProvider<OrganizationService> sutProvider)
    {
        RestoreRevokeUser_Setup(organization, owner, organizationUser, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();

        await sutProvider.Sut.RevokeUserAsync(organizationUser, eventSystemUser);

        await organizationUserRepository.Received().RevokeAsync(organizationUser.Id);
        await eventService.Received()
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Revoked, eventSystemUser);
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_Success(Organization organization, [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser, SutProvider<OrganizationService> sutProvider)
    {
        RestoreRevokeUser_Setup(organization, owner, organizationUser, sutProvider);
        var userService = Substitute.For<IUserService>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();

        await sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id, userService);

        await organizationUserRepository.Received().RestoreAsync(organizationUser.Id, OrganizationUserStatusType.Invited);
        await eventService.Received()
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored);
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_WithEventSystemUser_Success(Organization organization, [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser, EventSystemUser eventSystemUser, SutProvider<OrganizationService> sutProvider)
    {
        RestoreRevokeUser_Setup(organization, owner, organizationUser, sutProvider);
        var userService = Substitute.For<IUserService>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();

        await sutProvider.Sut.RestoreUserAsync(organizationUser, eventSystemUser, userService);

        await organizationUserRepository.Received().RestoreAsync(organizationUser.Id, OrganizationUserStatusType.Invited);
        await eventService.Received()
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored, eventSystemUser);
    }

    [Theory, BitAutoData]
    public async Task HasConfirmedOwnersExcept_WithConfirmedOwner_ReturnsTrue(Organization organization, [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner, SutProvider<OrganizationService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { owner });

        var result = await sutProvider.Sut.HasConfirmedOwnersExceptAsync(organization.Id, new List<Guid>(), true);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task HasConfirmedOwnersExcept_ExcludingConfirmedOwner_ReturnsFalse(Organization organization, [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner, SutProvider<OrganizationService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { owner });

        var result = await sutProvider.Sut.HasConfirmedOwnersExceptAsync(organization.Id, new List<Guid> { owner.Id }, true);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task HasConfirmedOwnersExcept_WithInvitedOwner_ReturnsFalse(Organization organization, [OrganizationUser(OrganizationUserStatusType.Invited, OrganizationUserType.Owner)] OrganizationUser owner, SutProvider<OrganizationService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new List<OrganizationUser> { owner });

        var result = await sutProvider.Sut.HasConfirmedOwnersExceptAsync(organization.Id, new List<Guid>(), true);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task HasConfirmedOwnersExcept_WithConfirmedProviderUser_IncludeProviderTrue_ReturnsTrue(bool includeProvider, Organization organization, ProviderUser providerUser, SutProvider<OrganizationService> sutProvider)
    {
        providerUser.Status = ProviderUserStatusType.Confirmed;

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByOrganizationAsync(organization.Id, ProviderUserStatusType.Confirmed)
            .Returns(new List<ProviderUser> { providerUser });

        var result = await sutProvider.Sut.HasConfirmedOwnersExceptAsync(organization.Id, new List<Guid>(), includeProvider);

        Assert.Equal(includeProvider, result);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually2019)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenInvalidPlanSelected(
        PlanType planType, SutProvider<OrganizationService> sutProvider)
    {
        var plan = StaticStore.Plans.FirstOrDefault(x => x.Type == planType);

        var signup = new OrganizationUpgrade
        {
            UseSecretsManager = true,
            AdditionalSmSeats = 1,
            AdditionalServiceAccounts = 10,
            AdditionalSeats = 1
        };

        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup));
        Assert.Contains("Invalid Secrets Manager plan selected.", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenNoSecretsManagerSeats(PlanType planType, SutProvider<OrganizationService> sutProvider)
    {
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == planType);
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
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == planType);
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
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == planType);
        var signup = new OrganizationUpgrade
        {
            UseSecretsManager = true,
            AdditionalSmSeats = 2,
            AdditionalServiceAccounts = 5,
            AdditionalSeats = 3
        };
        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup));
        Assert.Contains("Plan does not allow additional Service Accounts.", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenMoreSeatsThanPasswordManagerSeats(PlanType planType, SutProvider<OrganizationService> sutProvider)
    {
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == planType);
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
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenSubtractingServiceAccounts(
        PlanType planType,
        SutProvider<OrganizationService> sutProvider)
    {
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == planType);
        var signup = new OrganizationUpgrade
        {
            UseSecretsManager = true,
            AdditionalSmSeats = 4,
            AdditionalServiceAccounts = -5,
            AdditionalSeats = 5
        };
        var exception = Assert.Throws<BadRequestException>(() => sutProvider.Sut.ValidateSecretsManagerPlan(plan, signup));
        Assert.Contains("You can't subtract Service Accounts!", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.Free)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenPlanDoesNotAllowAdditionalUsers(
        PlanType planType,
        SutProvider<OrganizationService> sutProvider)
    {
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == planType);
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
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ValidPlan_NoExceptionThrown(
        PlanType planType,
        SutProvider<OrganizationService> sutProvider)
    {
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(x => x.Type == planType);
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
}
