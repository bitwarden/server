using System.Text.Json;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Models.Mail;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Core.Tokens;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Fakes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;
using Xunit;
using Organization = Bit.Core.AdminConsole.Entities.Organization;
using OrganizationUser = Bit.Core.Entities.OrganizationUser;

#nullable enable

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class OrganizationServiceTests
{
    private readonly IDataProtectorTokenFactory<OrgUserInviteTokenable> _orgUserInviteTokenDataFactory = new FakeDataProtectorTokenFactory<OrgUserInviteTokenable>();

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task OrgImportCreateNewUsers(SutProvider<OrganizationService> sutProvider, Organization org, List<OrganizationUserUserDetails> existingUsers, List<ImportedOrganizationUser> newUsers)
    {
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

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

        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);

        organizationUserRepository.GetManyDetailsByOrganizationAsync(org.Id)
            .Returns(existingUsers);
        organizationUserRepository.GetCountByOrganizationIdAsync(org.Id)
            .Returns(existingUsers.Count);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(org.Id, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(org.Id).Returns(true);

        // Mock tokenable factory to return a token that expires in 5 days
        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(
                info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
                {
                    ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
                }
            );

        await sutProvider.Sut.ImportAsync(org.Id, null, newUsers, null, false, EventSystemUser.PublicApi);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .UpsertManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => !users.Any()));
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default);

        // Create new users
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .CreateManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(users => users.Count() == expectedNewUsersCount));

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendOrganizationInviteEmailsAsync(
                Arg.Is<OrganizationInvitesInfo>(info => info.OrgUserTokenPairs.Count() == expectedNewUsersCount && info.IsFreeOrg == (org.PlanType == PlanType.Free) && info.OrganizationName == org.Name));

        // Send events
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventsAsync(Arg.Is<IEnumerable<(OrganizationUser, EventType, EventSystemUser, DateTime?)>>(events =>
            events.Count() == expectedNewUsersCount));
        await sutProvider.GetDependency<IReferenceEventService>().Received(1)
            .RaiseEventAsync(Arg.Is<ReferenceEvent>(referenceEvent =>
            referenceEvent.Type == ReferenceEventType.InvitedUsers && referenceEvent.Id == org.Id &&
            referenceEvent.Users == expectedNewUsersCount));
    }

    [Theory, PaidOrganizationCustomize, BitAutoData]
    public async Task OrgImportCreateNewUsersAndMarryExistingUser(SutProvider<OrganizationService> sutProvider, Organization org, List<OrganizationUserUserDetails> existingUsers,
        List<ImportedOrganizationUser> newUsers)
    {
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

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

        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(org.Id, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);

        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        currentContext.ManageUsers(org.Id).Returns(true);

        // Mock tokenable factory to return a token that expires in 5 days
        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(
                info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
                {
                    ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
                }
            );

        await sutProvider.Sut.ImportAsync(org.Id, null, newUsers, null, false, EventSystemUser.PublicApi);

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
            .SendOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Count() == expectedNewUsersCount && info.IsFreeOrg == (org.PlanType == PlanType.Free) && info.OrganizationName == org.Name));

        // Sent events
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventsAsync(Arg.Is<IEnumerable<(OrganizationUser, EventType, EventSystemUser, DateTime?)>>(events =>
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

        var plan = StaticStore.GetPlan(signup.Plan);

        signup.AdditionalSeats = 0;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;
        signup.IsFromSecretsManagerTrial = false;

        var purchaseOrganizationPlan = StaticStore.GetPlan(signup.Plan);

        var result = await sutProvider.Sut.SignUpAsync(signup);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).CreateAsync(
            Arg.Is<Organization>(o =>
                o.Seats == plan.PasswordManager.BaseSeats + signup.AdditionalSeats
                && o.SmSeats == null
                && o.SmServiceAccounts == null));
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).CreateAsync(
            Arg.Is<OrganizationUser>(o => o.AccessSecretsManager == signup.UseSecretsManager));

        await sutProvider.GetDependency<IReferenceEventService>().Received(1)
            .RaiseEventAsync(Arg.Is<ReferenceEvent>(referenceEvent =>
                referenceEvent.Type == ReferenceEventType.Signup &&
                referenceEvent.PlanName == plan.Name &&
                referenceEvent.PlanType == plan.Type &&
                referenceEvent.Seats == result.Item1.Seats &&
                referenceEvent.Storage == result.Item1.MaxStorageGb));
        // TODO: add reference events for SmSeats and Service Accounts - see AC-1481

        Assert.NotNull(result.Item1);
        Assert.NotNull(result.Item2);

        await sutProvider.GetDependency<IPaymentService>().Received(1).PurchaseOrganizationAsync(
            Arg.Any<Organization>(),
            signup.PaymentMethodType.Value,
            signup.PaymentToken,
            plan,
            signup.AdditionalStorageGb,
            signup.AdditionalSeats,
            signup.PremiumAccessAddon,
            signup.TaxInfo,
            false,
            signup.AdditionalSmSeats.GetValueOrDefault(),
            signup.AdditionalServiceAccounts.GetValueOrDefault(),
            signup.UseSecretsManager
        );
    }

    [Theory]
    [BitAutoData(PlanType.FamiliesAnnually)]
    public async Task SignUp_AssignsOwnerToDefaultCollection
        (PlanType planType, OrganizationSignup signup, SutProvider<OrganizationService> sutProvider)
    {
        signup.Plan = planType;
        signup.AdditionalSeats = 0;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.UseSecretsManager = false;

        // Extract orgUserId when created
        Guid? orgUserId = null;
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .CreateAsync(Arg.Do<OrganizationUser>(ou => orgUserId = ou.Id));

        var result = await sutProvider.Sut.SignUpAsync(signup);

        // Assert: created a Can Manage association for the default collection
        Assert.NotNull(orgUserId);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).CreateAsync(
            Arg.Any<Collection>(),
            Arg.Is<IEnumerable<CollectionAccessSelection>>(cas => cas == null),
            Arg.Is<IEnumerable<CollectionAccessSelection>>(cas =>
                cas.Count() == 1 &&
                cas.All(c =>
                    c.Id == orgUserId &&
                    !c.ReadOnly &&
                    !c.HidePasswords &&
                    c.Manage)));

        Assert.NotNull(result.Item1);
        Assert.NotNull(result.Item2);
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    public async Task SignUp_SM_Passes(PlanType planType, OrganizationSignup signup, SutProvider<OrganizationService> sutProvider)
    {
        signup.Plan = planType;

        var plan = StaticStore.GetPlan(signup.Plan);

        signup.UseSecretsManager = true;
        signup.AdditionalSeats = 15;
        signup.AdditionalSmSeats = 10;
        signup.AdditionalServiceAccounts = 20;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.IsFromSecretsManagerTrial = false;

        var result = await sutProvider.Sut.SignUpAsync(signup);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).CreateAsync(
            Arg.Is<Organization>(o =>
                o.Seats == plan.PasswordManager.BaseSeats + signup.AdditionalSeats
                && o.SmSeats == plan.SecretsManager.BaseSeats + signup.AdditionalSmSeats
                && o.SmServiceAccounts == plan.SecretsManager.BaseServiceAccount + signup.AdditionalServiceAccounts));
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).CreateAsync(
            Arg.Is<OrganizationUser>(o => o.AccessSecretsManager == signup.UseSecretsManager));

        await sutProvider.GetDependency<IReferenceEventService>().Received(1)
            .RaiseEventAsync(Arg.Is<ReferenceEvent>(referenceEvent =>
                referenceEvent.Type == ReferenceEventType.Signup &&
                referenceEvent.PlanName == plan.Name &&
                referenceEvent.PlanType == plan.Type &&
                referenceEvent.Seats == result.Item1.Seats &&
                referenceEvent.Storage == result.Item1.MaxStorageGb));
        // TODO: add reference events for SmSeats and Service Accounts - see AC-1481

        Assert.NotNull(result.Item1);
        Assert.NotNull(result.Item2);

        await sutProvider.GetDependency<IPaymentService>().Received(1).PurchaseOrganizationAsync(
            Arg.Any<Organization>(),
            signup.PaymentMethodType.Value,
            signup.PaymentToken,
            Arg.Is<Plan>(plan),
            signup.AdditionalStorageGb,
            signup.AdditionalSeats,
            signup.PremiumAccessAddon,
            signup.TaxInfo,
            false,
            signup.AdditionalSmSeats.GetValueOrDefault(),
            signup.AdditionalServiceAccounts.GetValueOrDefault(),
            signup.IsFromSecretsManagerTrial
        );
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    public async Task SignUp_SM_Throws_WhenManagedByMSP(PlanType planType, OrganizationSignup signup, SutProvider<OrganizationService> sutProvider)
    {
        signup.Plan = planType;
        signup.UseSecretsManager = true;
        signup.AdditionalSeats = 15;
        signup.AdditionalSmSeats = 10;
        signup.AdditionalServiceAccounts = 20;
        signup.PaymentMethodType = PaymentMethodType.Card;
        signup.PremiumAccessAddon = false;
        signup.IsFromProvider = true;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.SignUpAsync(signup));
        Assert.Contains("Organizations with a Managed Service Provider do not support Secrets Manager.", exception.Message);
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
        Assert.Contains("Plan does not allow additional Machine Accounts.", exception.Message);
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
        Assert.Contains("You can't subtract Machine Accounts!", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SignupClientAsync_Succeeds(
        OrganizationSignup signup,
        SutProvider<OrganizationService> sutProvider)
    {
        signup.Plan = PlanType.TeamsMonthly;

        var (organization, _, _) = await sutProvider.Sut.SignupClientAsync(signup);

        var plan = StaticStore.GetPlan(signup.Plan);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).CreateAsync(Arg.Is<Organization>(org =>
            org.Id == organization.Id &&
            org.Name == signup.Name &&
            org.Plan == plan.Name &&
            org.PlanType == plan.Type &&
            org.UsePolicies == plan.HasPolicies &&
            org.PublicKey == signup.PublicKey &&
            org.PrivateKey == signup.PrivateKey &&
            org.UseSecretsManager == false));

        await sutProvider.GetDependency<IOrganizationApiKeyRepository>().Received(1)
            .CreateAsync(Arg.Is<OrganizationApiKey>(orgApiKey =>
                orgApiKey.OrganizationId == organization.Id));

        await sutProvider.GetDependency<IApplicationCacheService>().Received(1)
            .UpsertOrganizationAbilityAsync(organization);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);

        await sutProvider.GetDependency<ICollectionRepository>().Received(1)
            .CreateAsync(Arg.Is<Collection>(c => c.Name == signup.CollectionName && c.OrganizationId == organization.Id), null, null);

        await sutProvider.GetDependency<IReferenceEventService>().Received(1).RaiseEventAsync(Arg.Is<ReferenceEvent>(
            re =>
                re.Type == ReferenceEventType.Signup &&
                re.PlanType == plan.Type));
    }

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

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        organizationUserRepository.GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new[] { owner });

        // Must set guids in order for dictionary of guids to not throw aggregate exceptions
        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);

        // Mock tokenable factory to return a token that expires in 5 days
        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(
                info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
                {
                    ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
                }
                );


        await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, systemUser: null, new (OrganizationUserInvite, string)[] { (invite, null) });

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Count() == invite.Emails.Distinct().Count() &&
                info.IsFreeOrg == (organization.PlanType == PlanType.Free) &&
                info.OrganizationName == organization.Name));

    }

    [Theory]
    [OrganizationInviteCustomize, OrganizationCustomize, BitAutoData]
    public async Task InviteUsers_SsoOrgWithNullSsoConfig_Passes(Organization organization, OrganizationUser invitor,
                [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        OrganizationUserInvite invite, SutProvider<OrganizationService> sutProvider)
    {
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        // Org must be able to use SSO to trigger this proper test case as we currently only call to retrieve
        // an org's SSO config if the org can use SSO
        organization.UseSso = true;

        // Return null for sso config
        sutProvider.GetDependency<ISsoConfigRepository>().GetByOrganizationIdAsync(organization.Id).ReturnsNull();

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        organizationUserRepository.GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new[] { owner });

        // Must set guids in order for dictionary of guids to not throw aggregate exceptions
        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);

        // Mock tokenable factory to return a token that expires in 5 days
        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(
                info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
                {
                    ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
                }
                );



        await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, systemUser: null, new (OrganizationUserInvite, string)[] { (invite, null) });

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Count() == invite.Emails.Distinct().Count() &&
                info.IsFreeOrg == (organization.PlanType == PlanType.Free) &&
                info.OrganizationName == organization.Name));
    }

    [Theory]
    [OrganizationInviteCustomize, OrganizationCustomize, BitAutoData]
    public async Task InviteUsers_SsoOrgWithNeverEnabledRequireSsoPolicy_Passes(Organization organization, SsoConfig ssoConfig, OrganizationUser invitor,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
OrganizationUserInvite invite, SutProvider<OrganizationService> sutProvider)
    {
        // Setup FakeDataProtectorTokenFactory for creating new tokens - this must come first in order to avoid resetting mocks
        sutProvider.SetDependency(_orgUserInviteTokenDataFactory, "orgUserInviteTokenDataFactory");
        sutProvider.Create();

        // Org must be able to use SSO and policies to trigger this test case
        organization.UseSso = true;
        organization.UsePolicies = true;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(true);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        organizationUserRepository.GetManyByOrganizationAsync(organization.Id, OrganizationUserType.Owner)
            .Returns(new[] { owner });

        ssoConfig.Enabled = true;
        sutProvider.GetDependency<ISsoConfigRepository>().GetByOrganizationIdAsync(organization.Id).Returns(ssoConfig);


        // Return null policy to mimic new org that's never turned on the require sso policy
        sutProvider.GetDependency<IPolicyRepository>().GetManyByOrganizationIdAsync(organization.Id).ReturnsNull();

        // Must set guids in order for dictionary of guids to not throw aggregate exceptions
        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);

        // Mock tokenable factory to return a token that expires in 5 days
        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(
                info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
                {
                    ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
                }
                );

        await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, systemUser: null, new (OrganizationUserInvite, string)[] { (invite, null) });

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Count() == invite.Emails.Distinct().Count() &&
                info.IsFreeOrg == (organization.PlanType == PlanType.Free) &&
                info.OrganizationName == organization.Name));
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
        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(
                info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
                {
                    ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
                }
            );

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organization.Id, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);
        SetupOrgUserRepositoryCreateAsyncMock(organizationUserRepository);

        await sutProvider.Sut.InviteUserAsync(organization.Id, invitor.UserId, systemUser: null, invite, externalId);

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Count() == 1 &&
                info.IsFreeOrg == (organization.PlanType == PlanType.Free) &&
                info.OrganizationName == organization.Name));

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

        // Mock tokenable factory to return a token that expires in 5 days
        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(
                info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
                {
                    ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
                }
            );

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organization.Id, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);
        SetupOrgUserRepositoryCreateAsyncMock(organizationUserRepository);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut
            .InviteUserAsync(organization.Id, invitor.UserId, systemUser: null, invite, externalId));
        Assert.Contains("This user has already been invited", exception.Message);

        // MailService and EventService are still called, but with no OrgUsers
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                !info.OrgUserTokenPairs.Any() &&
                info.IsFreeOrg == (organization.PlanType == PlanType.Free) &&
                info.OrganizationName == organization.Name));
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

        // Mock tokenable factory to return a token that expires in 5 days
        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(
                info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
                {
                    ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
                }
            );

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organization.Id, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);

        SetupOrgUserRepositoryCreateManyAsyncMock(organizationUserRepository);
        SetupOrgUserRepositoryCreateAsyncMock(organizationUserRepository);

        await sutProvider.Sut.InviteUsersAsync(organization.Id, invitor.UserId, systemUser: null, invites);

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Count() == invites.SelectMany(i => i.invite.Emails).Count() &&
                info.IsFreeOrg == (organization.PlanType == PlanType.Free) &&
                info.OrganizationName == organization.Name));

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

        // Mock tokenable factory to return a token that expires in 5 days
        sutProvider.GetDependency<IOrgUserInviteTokenableFactory>()
            .CreateToken(Arg.Any<OrganizationUser>())
            .Returns(
                info => new OrgUserInviteTokenable(info.Arg<OrganizationUser>())
                {
                    ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromDays(5))
                }
            );

        await sutProvider.Sut.InviteUsersAsync(organization.Id, invitingUserId: null, eventSystemUser, invites);

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendOrganizationInviteEmailsAsync(Arg.Is<OrganizationInvitesInfo>(info =>
                info.OrgUserTokenPairs.Count() == invites.SelectMany(i => i.invite.Emails).Count() &&
                info.IsFreeOrg == (organization.PlanType == PlanType.Free) &&
                info.OrganizationName == organization.Name));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventsAsync(Arg.Any<IEnumerable<(OrganizationUser, EventType, EventSystemUser, DateTime?)>>());
    }

    [Theory, BitAutoData, OrganizationCustomize, OrganizationInviteCustomize]
    public async Task InviteUsers_WithSecretsManager_Passes(Organization organization,
        IEnumerable<(OrganizationUserInvite invite, string externalId)> invites,
        OrganizationUser savingUser, SutProvider<OrganizationService> sutProvider)
    {
        organization.PlanType = PlanType.EnterpriseAnnually;
        InviteUserHelper_ArrangeValidPermissions(organization, savingUser, sutProvider);

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

        // Assume we need to add seats for all invited SM users
        sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>()
            .CountNewSmSeatsRequiredAsync(organization.Id, invitedSmUsers).Returns(invitedSmUsers);

        // Mock SecretsManagerSubscriptionUpdateCommand to actually change the organization's subscription in memory
        sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .UpdateSubscriptionAsync(Arg.Any<SecretsManagerSubscriptionUpdate>())
            .ReturnsForAnyArgs(Task.FromResult(0)).AndDoes(x => organization.SmSeats += invitedSmUsers);

        // Throw error at the end of the try block
        sutProvider.GetDependency<IReferenceEventService>().RaiseEventAsync(default)
            .ThrowsForAnyArgs<BadRequestException>();

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

    [Theory, BitAutoData]
    public async Task ConfirmUser_InvalidStatus(OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Invited)] OrganizationUser orgUser, string key,
        SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        organizationUserRepository.GetByIdAsync(orgUser.Id).Returns(orgUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));
        Assert.Contains("User not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUser_WrongOrganization(OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, string key,
        SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        organizationUserRepository.GetByIdAsync(orgUser.Id).Returns(orgUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(confirmingUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));
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
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));
        Assert.Contains("User can only be an admin of one free organization.", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.Custom, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.Custom, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.EnterpriseAnnually, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.EnterpriseAnnually, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.EnterpriseAnnually2020, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.EnterpriseAnnually2020, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.EnterpriseAnnually2019, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.EnterpriseAnnually2019, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.EnterpriseMonthly, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.EnterpriseMonthly, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.EnterpriseMonthly2020, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.EnterpriseMonthly2020, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.EnterpriseMonthly2019, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.EnterpriseMonthly2019, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.FamiliesAnnually, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.FamiliesAnnually, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.FamiliesAnnually2019, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.FamiliesAnnually2019, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.TeamsAnnually, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.TeamsAnnually, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.TeamsAnnually2020, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.TeamsAnnually2020, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.TeamsAnnually2019, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.TeamsAnnually2019, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.TeamsMonthly, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.TeamsMonthly, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.TeamsMonthly2020, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.TeamsMonthly2020, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.TeamsMonthly2019, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.TeamsMonthly2019, OrganizationUserType.Owner)]
    public async Task ConfirmUserToNonFree_AlreadyFreeAdminOrOwner_DoesNotThrow(PlanType planType, OrganizationUserType orgUserType, Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        string key, SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();

        org.PlanType = planType;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;
        orgUser.Type = orgUserType;
        orgUser.AccessSecretsManager = false;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(orgUser.UserId.Value).Returns(1);
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
        await sutProvider.GetDependency<IMailService>().Received(1).SendOrganizationConfirmedEmailAsync(org.DisplayName(), user.Email);
        await organizationUserRepository.Received(1).ReplaceManyAsync(Arg.Is<List<OrganizationUser>>(users => users.Contains(orgUser) && users.Count == 1));
    }


    [Theory, BitAutoData]
    public async Task ConfirmUser_AsUser_SingleOrgPolicy_AppliedFromConfirmingOrg_Throws(Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        OrganizationUser orgUserAnotherOrg, [OrganizationUserPolicyDetails(PolicyType.SingleOrg)] OrganizationUserPolicyDetails singleOrgPolicy,
        string key, SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var policyService = sutProvider.GetDependency<IPolicyService>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.Status = OrganizationUserStatusType.Accepted;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] { orgUserAnotherOrg });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });
        singleOrgPolicy.OrganizationId = org.Id;
        policyService.GetPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg).Returns(new[] { singleOrgPolicy });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));
        Assert.Contains("Cannot confirm this member to the organization until they leave or remove all other organizations.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUser_AsUser_SingleOrgPolicy_AppliedFromOtherOrg_Throws(Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        OrganizationUser orgUserAnotherOrg, [OrganizationUserPolicyDetails(PolicyType.SingleOrg)] OrganizationUserPolicyDetails singleOrgPolicy,
        string key, SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var policyService = sutProvider.GetDependency<IPolicyService>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.Status = OrganizationUserStatusType.Accepted;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] { orgUserAnotherOrg });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });
        singleOrgPolicy.OrganizationId = orgUserAnotherOrg.Id;
        policyService.GetPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg).Returns(new[] { singleOrgPolicy });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));
        Assert.Contains("Cannot confirm this member to the organization because they are in another organization which forbids it.", exception.Message);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task ConfirmUser_AsOwnerOrAdmin_SingleOrgPolicy_ExcludedViaUserType_Success(
        OrganizationUserType userType, Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        OrganizationUser orgUserAnotherOrg,
        string key, SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.Type = userType;
        orgUser.Status = OrganizationUserStatusType.Accepted;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
        orgUser.AccessSecretsManager = true;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] { orgUserAnotherOrg });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
        await sutProvider.GetDependency<IMailService>().Received(1).SendOrganizationConfirmedEmailAsync(org.DisplayName(), user.Email, true);
        await organizationUserRepository.Received(1).ReplaceManyAsync(Arg.Is<List<OrganizationUser>>(users => users.Contains(orgUser) && users.Count == 1));
    }

    [Theory, BitAutoData]
    public async Task ConfirmUser_TwoFactorPolicy_NotEnabled_Throws(Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        OrganizationUser orgUserAnotherOrg,
        [OrganizationUserPolicyDetails(PolicyType.TwoFactorAuthentication)] OrganizationUserPolicyDetails twoFactorPolicy,
        string key, SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var policyService = sutProvider.GetDependency<IPolicyService>();
        var twoFactorIsEnabledQuery = sutProvider.GetDependency<ITwoFactorIsEnabledQuery>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] { orgUserAnotherOrg });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });
        twoFactorPolicy.OrganizationId = org.Id;
        policyService.GetPoliciesApplicableToUserAsync(user.Id, PolicyType.TwoFactorAuthentication).Returns(new[] { twoFactorPolicy });
        twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user.Id)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (user.Id, false) });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));
        Assert.Contains("User does not have two-step login enabled.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUser_TwoFactorPolicy_Enabled_Success(Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        [OrganizationUserPolicyDetails(PolicyType.TwoFactorAuthentication)] OrganizationUserPolicyDetails twoFactorPolicy,
        string key, SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var policyService = sutProvider.GetDependency<IPolicyService>();
        var twoFactorIsEnabledQuery = sutProvider.GetDependency<ITwoFactorIsEnabledQuery>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });
        twoFactorPolicy.OrganizationId = org.Id;
        policyService.GetPoliciesApplicableToUserAsync(user.Id, PolicyType.TwoFactorAuthentication).Returns(new[] { twoFactorPolicy });
        twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user.Id)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (user.Id, true) });

        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUsers_Success(Organization org,
        OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser2,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser3,
        OrganizationUser anotherOrgUser, User user1, User user2, User user3,
        [OrganizationUserPolicyDetails(PolicyType.TwoFactorAuthentication)] OrganizationUserPolicyDetails twoFactorPolicy,
        [OrganizationUserPolicyDetails(PolicyType.SingleOrg)] OrganizationUserPolicyDetails singleOrgPolicy,
        string key, SutProvider<OrganizationService> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var policyService = sutProvider.GetDependency<IPolicyService>();
        var twoFactorIsEnabledQuery = sutProvider.GetDependency<ITwoFactorIsEnabledQuery>();

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
        twoFactorPolicy.OrganizationId = org.Id;
        policyService.GetPoliciesApplicableToUserAsync(Arg.Any<Guid>(), PolicyType.TwoFactorAuthentication).Returns(new[] { twoFactorPolicy });
        twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user1.Id) && ids.Contains(user2.Id) && ids.Contains(user3.Id)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>()
            {
                (user1.Id, true),
                (user2.Id, false),
                (user3.Id, true)
            });
        singleOrgPolicy.OrganizationId = org.Id;
        policyService.GetPoliciesApplicableToUserAsync(user3.Id, PolicyType.SingleOrg)
            .Returns(new[] { singleOrgPolicy });
        organizationUserRepository.GetManyByManyUsersAsync(default)
            .ReturnsForAnyArgs(new[] { orgUser1, orgUser2, orgUser3, anotherOrgUser });

        var keys = orgUsers.ToDictionary(ou => ou.Id, _ => key);
        var result = await sutProvider.Sut.ConfirmUsersAsync(confirmingUser.OrganizationId, keys, confirmingUser.Id);
        Assert.Contains("", result[0].Item2);
        Assert.Contains("User does not have two-step login enabled.", result[1].Item2);
        Assert.Contains("Cannot confirm this member to the organization until they leave or remove all other organizations.", result[2].Item2);
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

    private void RestoreRevokeUser_Setup(
        Organization organization,
        OrganizationUser? requestingOrganizationUser,
        OrganizationUser targetOrganizationUser,
        SutProvider<OrganizationService> sutProvider)
    {
        if (requestingOrganizationUser != null)
        {
            requestingOrganizationUser.OrganizationId = organization.Id;
        }
        targetOrganizationUser.OrganizationId = organization.Id;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(requestingOrganizationUser != null && requestingOrganizationUser.Type is OrganizationUserType.Owner);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(requestingOrganizationUser != null && (requestingOrganizationUser.Type is OrganizationUserType.Owner or OrganizationUserType.Admin));
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organization.Id, Arg.Any<IEnumerable<Guid>>())
            .Returns(true);
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
    public async Task RevokeUser_WithEventSystemUser_Success(Organization organization, [OrganizationUser] OrganizationUser organizationUser, EventSystemUser eventSystemUser, SutProvider<OrganizationService> sutProvider)
    {
        RestoreRevokeUser_Setup(organization, null, organizationUser, sutProvider);
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
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();

        await sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id);

        await organizationUserRepository.Received().RestoreAsync(organizationUser.Id, OrganizationUserStatusType.Invited);
        await eventService.Received()
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored);
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_WithEventSystemUser_Success(Organization organization, [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser, EventSystemUser eventSystemUser, SutProvider<OrganizationService> sutProvider)
    {
        RestoreRevokeUser_Setup(organization, null, organizationUser, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();

        await sutProvider.Sut.RestoreUserAsync(organizationUser, eventSystemUser);

        await organizationUserRepository.Received().RestoreAsync(organizationUser.Id, OrganizationUserStatusType.Invited);
        await eventService.Received()
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored, eventSystemUser);
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_RestoreThemselves_Fails(Organization organization, [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser, SutProvider<OrganizationService> sutProvider)
    {
        organizationUser.UserId = owner.Id;
        RestoreRevokeUser_Setup(organization, owner, organizationUser, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id));

        Assert.Contains("you cannot restore yourself", exception.Message.ToLowerInvariant());

        await organizationUserRepository.DidNotReceiveWithAnyArgs().RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await eventService.DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task RestoreUser_AdminRestoreOwner_Fails(OrganizationUserType restoringUserType,
        Organization organization, [OrganizationUser(OrganizationUserStatusType.Confirmed)] OrganizationUser restoringUser,
        [OrganizationUser(OrganizationUserStatusType.Revoked, OrganizationUserType.Owner)] OrganizationUser organizationUser, SutProvider<OrganizationService> sutProvider)
    {
        restoringUser.Type = restoringUserType;
        RestoreRevokeUser_Setup(organization, restoringUser, organizationUser, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, restoringUser.Id));

        Assert.Contains("only owners can restore other owners", exception.Message.ToLowerInvariant());

        await organizationUserRepository.DidNotReceiveWithAnyArgs().RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await eventService.DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public async Task RestoreUser_WithStatusOtherThanRevoked_Fails(OrganizationUserStatusType userStatus, Organization organization, [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser] OrganizationUser organizationUser, SutProvider<OrganizationService> sutProvider)
    {
        organizationUser.Status = userStatus;
        RestoreRevokeUser_Setup(organization, owner, organizationUser, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id));

        Assert.Contains("already active", exception.Message.ToLowerInvariant());

        await organizationUserRepository.DidNotReceiveWithAnyArgs().RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await eventService.DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_WithOtherOrganizationSingleOrgPolicyEnabled_Fails(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        SutProvider<OrganizationService> sutProvider)
    {
        organizationUser.Email = null; // this is required to mock that the user as had already been confirmed before the revoke
        RestoreRevokeUser_Setup(organization, owner, organizationUser, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.SingleOrg, Arg.Any<OrganizationUserStatusType>())
            .Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id));

        Assert.Contains("you cannot restore this user because they are a member of " +
                        "another organization which forbids it", exception.Message.ToLowerInvariant());

        await organizationUserRepository.DidNotReceiveWithAnyArgs().RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await eventService.DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_With2FAPolicyEnabled_WithoutUser2FAConfigured_Fails(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        SutProvider<OrganizationService> sutProvider)
    {
        organizationUser.Email = null;

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.UserId.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (organizationUser.UserId.Value, false) });

        RestoreRevokeUser_Setup(organization, owner, organizationUser, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();

        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.TwoFactorAuthentication, Arg.Any<OrganizationUserStatusType>())
            .Returns(new[] { new OrganizationUserPolicyDetails { OrganizationId = organizationUser.OrganizationId, PolicyType = PolicyType.TwoFactorAuthentication } });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id));

        Assert.Contains("you cannot restore this user until they enable " +
                        "two-step login on their user account.", exception.Message.ToLowerInvariant());

        await organizationUserRepository.DidNotReceiveWithAnyArgs().RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await eventService.DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_With2FAPolicyEnabled_WithUser2FAConfigured_Success(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        SutProvider<OrganizationService> sutProvider)
    {
        organizationUser.Email = null; // this is required to mock that the user as had already been confirmed before the revoke
        RestoreRevokeUser_Setup(organization, owner, organizationUser, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();

        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.TwoFactorAuthentication, Arg.Any<OrganizationUserStatusType>())
            .Returns(new[] { new OrganizationUserPolicyDetails { OrganizationId = organizationUser.OrganizationId, PolicyType = PolicyType.TwoFactorAuthentication } });
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.UserId.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (organizationUser.UserId.Value, true) });

        await sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id);

        await organizationUserRepository.Received().RestoreAsync(organizationUser.Id, OrganizationUserStatusType.Confirmed);
        await eventService.Received()
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored);
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_WithSingleOrgPolicyEnabled_Fails(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser secondOrganizationUser,
        SutProvider<OrganizationService> sutProvider)
    {
        organizationUser.Email = null; // this is required to mock that the user as had already been confirmed before the revoke
        secondOrganizationUser.UserId = organizationUser.UserId;
        RestoreRevokeUser_Setup(organization, owner, organizationUser, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();

        organizationUserRepository.GetManyByUserAsync(organizationUser.UserId.Value).Returns(new[] { organizationUser, secondOrganizationUser });
        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.SingleOrg, Arg.Any<OrganizationUserStatusType>())
            .Returns(new[]
            {
                new OrganizationUserPolicyDetails { OrganizationId = organizationUser.OrganizationId, PolicyType = PolicyType.SingleOrg, OrganizationUserStatus = OrganizationUserStatusType.Revoked }
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id));

        Assert.Contains("you cannot restore this user until " +
                        "they leave or remove all other organizations.", exception.Message.ToLowerInvariant());

        await organizationUserRepository.DidNotReceiveWithAnyArgs().RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await eventService.DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_vNext_WithOtherOrganizationSingleOrgPolicyEnabled_Fails(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser secondOrganizationUser,
        SutProvider<OrganizationService> sutProvider)
    {
        organizationUser.Email = null; // this is required to mock that the user as had already been confirmed before the revoke
        secondOrganizationUser.UserId = organizationUser.UserId;
        RestoreRevokeUser_Setup(organization, owner, organizationUser, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();
        var twoFactorIsEnabledQuery = sutProvider.GetDependency<ITwoFactorIsEnabledQuery>();

        twoFactorIsEnabledQuery
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.UserId.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (organizationUser.UserId.Value, true) });

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.SingleOrg, Arg.Any<OrganizationUserStatusType>())
            .Returns(true);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id));

        Assert.Contains("you cannot restore this user because they are a member of " +
                        "another organization which forbids it", exception.Message.ToLowerInvariant());

        await organizationUserRepository.DidNotReceiveWithAnyArgs().RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await eventService.DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_vNext_With2FAPolicyEnabled_WithoutUser2FAConfigured_Fails(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        SutProvider<OrganizationService> sutProvider)
    {
        organizationUser.Email = null;

        RestoreRevokeUser_Setup(organization, owner, organizationUser, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();

        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.TwoFactorAuthentication, Arg.Any<OrganizationUserStatusType>())
            .Returns(new[] { new OrganizationUserPolicyDetails { OrganizationId = organizationUser.OrganizationId, PolicyType = PolicyType.TwoFactorAuthentication } });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id));

        Assert.Contains("you cannot restore this user until they enable " +
                        "two-step login on their user account.", exception.Message.ToLowerInvariant());

        await organizationUserRepository.DidNotReceiveWithAnyArgs().RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await eventService.DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_vNext_With2FAPolicyEnabled_WithUser2FAConfigured_Success(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        SutProvider<OrganizationService> sutProvider)
    {
        organizationUser.Email = null; // this is required to mock that the user as had already been confirmed before the revoke
        RestoreRevokeUser_Setup(organization, owner, organizationUser, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();
        var twoFactorIsEnabledQuery = sutProvider.GetDependency<ITwoFactorIsEnabledQuery>();

        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.TwoFactorAuthentication, Arg.Any<OrganizationUserStatusType>())
            .Returns(new[] { new OrganizationUserPolicyDetails { OrganizationId = organizationUser.OrganizationId, PolicyType = PolicyType.TwoFactorAuthentication } });

        twoFactorIsEnabledQuery
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.UserId.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (organizationUser.UserId.Value, true) });

        await sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id);

        await organizationUserRepository.Received().RestoreAsync(organizationUser.Id, OrganizationUserStatusType.Confirmed);
        await eventService.Received()
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored);
    }

    [Theory]
    [BitAutoData(PlanType.TeamsAnnually)]
    [BitAutoData(PlanType.TeamsMonthly)]
    [BitAutoData(PlanType.TeamsStarter)]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    public void ValidateSecretsManagerPlan_ThrowsException_WhenNoSecretsManagerSeats(PlanType planType, SutProvider<OrganizationService> sutProvider)
    {
        var plan = StaticStore.GetPlan(planType);
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
        var plan = StaticStore.GetPlan(planType);
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
        var plan = StaticStore.GetPlan(planType);
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
        var plan = StaticStore.GetPlan(planType);
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
        var plan = StaticStore.GetPlan(planType);
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
        var plan = StaticStore.GetPlan(planType);
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
        var plan = StaticStore.GetPlan(planType);
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
