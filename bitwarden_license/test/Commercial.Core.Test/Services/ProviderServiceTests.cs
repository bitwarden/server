using Bit.Commercial.Core.Services;
using Bit.Commercial.Core.Test.AutoFixture;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Entities.Provider;
using Bit.Core.Enums;
using Bit.Core.Enums.Provider;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Business.Provider;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;
using Provider = Bit.Core.Entities.Provider.Provider;
using ProviderUser = Bit.Core.Entities.Provider.ProviderUser;

namespace Bit.Commercial.Core.Test.Services;

[SutProviderCustomize]
public class ProviderServiceTests
{
    [Theory, BitAutoData]
    public async Task CompleteSetupAsync_UserIdIsInvalid_Throws(SutProvider<ProviderService> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.CompleteSetupAsync(default, default, default, default));
        Assert.Contains("Invalid owner.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task CompleteSetupAsync_TokenIsInvalid_Throws(User user, Provider provider,
        SutProvider<ProviderService> sutProvider)
    {
        var userService = sutProvider.GetDependency<IUserService>();
        userService.GetUserByIdAsync(user.Id).Returns(user);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.CompleteSetupAsync(provider, user.Id, default, default));
        Assert.Contains("Invalid token.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task CompleteSetupAsync_Success(User user, Provider provider, string key,
        [ProviderUser(ProviderUserStatusType.Confirmed, ProviderUserType.ProviderAdmin)] ProviderUser providerUser,
        SutProvider<ProviderService> sutProvider)
    {
        providerUser.ProviderId = provider.Id;
        providerUser.UserId = user.Id;
        var userService = sutProvider.GetDependency<IUserService>();
        userService.GetUserByIdAsync(user.Id).Returns(user);

        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        providerUserRepository.GetByProviderUserAsync(provider.Id, user.Id).Returns(providerUser);

        var dataProtectionProvider = DataProtectionProvider.Create("ApplicationName");
        var protector = dataProtectionProvider.CreateProtector("ProviderServiceDataProtector");
        sutProvider.GetDependency<IDataProtectionProvider>().CreateProtector("ProviderServiceDataProtector")
            .Returns(protector);
        sutProvider.Create();

        var token = protector.Protect($"ProviderSetupInvite {provider.Id} {user.Email} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}");

        await sutProvider.Sut.CompleteSetupAsync(provider, user.Id, token, key);

        await sutProvider.GetDependency<IProviderRepository>().Received().UpsertAsync(provider);
        await sutProvider.GetDependency<IProviderUserRepository>().Received()
            .ReplaceAsync(Arg.Is<ProviderUser>(pu => pu.UserId == user.Id && pu.ProviderId == provider.Id && pu.Key == key));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ProviderIdIsInvalid_Throws(Provider provider, SutProvider<ProviderService> sutProvider)
    {
        provider.Id = default;

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => sutProvider.Sut.UpdateAsync(provider));
        Assert.Contains("Cannot create provider this way.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_Success(Provider provider, SutProvider<ProviderService> sutProvider)
    {
        await sutProvider.Sut.UpdateAsync(provider);
    }

    [Theory, BitAutoData]
    public async Task InviteUserAsync_ProviderIdIsInvalid_Throws(ProviderUserInvite<string> invite, SutProvider<ProviderService> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ProviderManageUsers(invite.ProviderId).Returns(true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.InviteUserAsync(invite));
    }

    [Theory, BitAutoData]
    public async Task InviteUserAsync_InvalidPermissions_Throws(ProviderUserInvite<string> invite, SutProvider<ProviderService> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ProviderManageUsers(invite.ProviderId).Returns(false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.InviteUserAsync(invite));
    }

    [Theory, BitAutoData]
    public async Task InviteUserAsync_EmailsInvalid_Throws(Provider provider, ProviderUserInvite<string> providerUserInvite,
        SutProvider<ProviderService> sutProvider)
    {
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        providerRepository.GetByIdAsync(providerUserInvite.ProviderId).Returns(provider);
        sutProvider.GetDependency<ICurrentContext>().ProviderManageUsers(providerUserInvite.ProviderId).Returns(true);

        providerUserInvite.UserIdentifiers = null;

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.InviteUserAsync(providerUserInvite));
    }

    [Theory, BitAutoData]
    public async Task InviteUserAsync_AlreadyInvited(Provider provider, ProviderUserInvite<string> providerUserInvite,
        SutProvider<ProviderService> sutProvider)
    {
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        providerRepository.GetByIdAsync(providerUserInvite.ProviderId).Returns(provider);
        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        providerUserRepository.GetCountByProviderAsync(default, default, default).ReturnsForAnyArgs(1);
        sutProvider.GetDependency<ICurrentContext>().ProviderManageUsers(providerUserInvite.ProviderId).Returns(true);

        var result = await sutProvider.Sut.InviteUserAsync(providerUserInvite);
        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task InviteUserAsync_Success(Provider provider, ProviderUserInvite<string> providerUserInvite,
        SutProvider<ProviderService> sutProvider)
    {
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        providerRepository.GetByIdAsync(providerUserInvite.ProviderId).Returns(provider);
        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        providerUserRepository.GetCountByProviderAsync(default, default, default).ReturnsForAnyArgs(0);
        sutProvider.GetDependency<ICurrentContext>().ProviderManageUsers(providerUserInvite.ProviderId).Returns(true);

        var result = await sutProvider.Sut.InviteUserAsync(providerUserInvite);
        Assert.Equal(providerUserInvite.UserIdentifiers.Count(), result.Count);
        Assert.True(result.TrueForAll(pu => pu.Status == ProviderUserStatusType.Invited), "Status must be invited");
        Assert.True(result.TrueForAll(pu => pu.ProviderId == providerUserInvite.ProviderId), "Provider Id must be correct");
    }

    [Theory, BitAutoData]
    public async Task ResendInviteUserAsync_InvalidPermissions_Throws(ProviderUserInvite<Guid> invite, SutProvider<ProviderService> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ProviderManageUsers(invite.ProviderId).Returns(false);
        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ResendInvitesAsync(invite));
    }

    [Theory, BitAutoData]
    public async Task ResendInvitesAsync_Errors(Provider provider,
        [ProviderUser(ProviderUserStatusType.Invited)] ProviderUser pu1,
        [ProviderUser(ProviderUserStatusType.Accepted)] ProviderUser pu2,
        [ProviderUser(ProviderUserStatusType.Confirmed)] ProviderUser pu3,
        [ProviderUser(ProviderUserStatusType.Invited)] ProviderUser pu4,
        SutProvider<ProviderService> sutProvider)
    {
        var providerUsers = new[] { pu1, pu2, pu3, pu4 };
        pu1.ProviderId = pu2.ProviderId = pu3.ProviderId = provider.Id;

        var invite = new ProviderUserInvite<Guid>
        {
            UserIdentifiers = providerUsers.Select(pu => pu.Id),
            ProviderId = provider.Id
        };

        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        providerRepository.GetByIdAsync(provider.Id).Returns(provider);
        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        providerUserRepository.GetManyAsync(default).ReturnsForAnyArgs(providerUsers.ToList());
        sutProvider.GetDependency<ICurrentContext>().ProviderManageUsers(invite.ProviderId).Returns(true);

        var result = await sutProvider.Sut.ResendInvitesAsync(invite);
        Assert.Equal("", result[0].Item2);
        Assert.Equal("User invalid.", result[1].Item2);
        Assert.Equal("User invalid.", result[2].Item2);
        Assert.Equal("User invalid.", result[3].Item2);
    }

    [Theory, BitAutoData]
    public async Task ResendInvitesAsync_Success(Provider provider, IEnumerable<ProviderUser> providerUsers,
        SutProvider<ProviderService> sutProvider)
    {
        foreach (var providerUser in providerUsers)
        {
            providerUser.ProviderId = provider.Id;
            providerUser.Status = ProviderUserStatusType.Invited;
        }

        var invite = new ProviderUserInvite<Guid>
        {
            UserIdentifiers = providerUsers.Select(pu => pu.Id),
            ProviderId = provider.Id
        };

        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        providerRepository.GetByIdAsync(provider.Id).Returns(provider);
        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        providerUserRepository.GetManyAsync(default).ReturnsForAnyArgs(providerUsers.ToList());
        sutProvider.GetDependency<ICurrentContext>().ProviderManageUsers(invite.ProviderId).Returns(true);

        var result = await sutProvider.Sut.ResendInvitesAsync(invite);
        Assert.True(result.All(r => r.Item2 == ""));
    }

    [Theory, BitAutoData]
    public async Task SendProviderSetupInviteEmailAsync_Success(Provider provider, string email, SutProvider<ProviderService> sutProvider)
    {
        await sutProvider.Sut.SendProviderSetupInviteEmailAsync(provider, email);

        await sutProvider.GetDependency<IMailService>().Received(1).SendProviderSetupInviteEmailAsync(provider, Arg.Any<string>(), email);
    }

    [Theory, BitAutoData]
    public async Task AcceptUserAsync_UserIsInvalid_Throws(SutProvider<ProviderService> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptUserAsync(default, default, default));
        Assert.Equal("User invalid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task AcceptUserAsync_AlreadyAccepted_Throws(
        [ProviderUser(ProviderUserStatusType.Accepted)] ProviderUser providerUser, User user,
        SutProvider<ProviderService> sutProvider)
    {
        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        providerUserRepository.GetByIdAsync(providerUser.Id).Returns(providerUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptUserAsync(providerUser.Id, user, default));
        Assert.Equal("Already accepted.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task AcceptUserAsync_TokenIsInvalid_Throws(
        [ProviderUser(ProviderUserStatusType.Invited)] ProviderUser providerUser, User user,
        SutProvider<ProviderService> sutProvider)
    {
        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        providerUserRepository.GetByIdAsync(providerUser.Id).Returns(providerUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptUserAsync(providerUser.Id, user, default));
        Assert.Equal("Invalid token.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task AcceptUserAsync_WrongEmail_Throws(
        [ProviderUser(ProviderUserStatusType.Invited)] ProviderUser providerUser, User user,
        SutProvider<ProviderService> sutProvider)
    {
        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        providerUserRepository.GetByIdAsync(providerUser.Id).Returns(providerUser);

        var dataProtectionProvider = DataProtectionProvider.Create("ApplicationName");
        var protector = dataProtectionProvider.CreateProtector("ProviderServiceDataProtector");
        sutProvider.GetDependency<IDataProtectionProvider>().CreateProtector("ProviderServiceDataProtector")
            .Returns(protector);
        sutProvider.Create();

        var token = protector.Protect($"ProviderUserInvite {providerUser.Id} {user.Email} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}");

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AcceptUserAsync(providerUser.Id, user, token));
        Assert.Equal("User email does not match invite.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task AcceptUserAsync_Success(
        [ProviderUser(ProviderUserStatusType.Invited)] ProviderUser providerUser, User user,
        SutProvider<ProviderService> sutProvider)
    {
        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        providerUserRepository.GetByIdAsync(providerUser.Id).Returns(providerUser);

        var dataProtectionProvider = DataProtectionProvider.Create("ApplicationName");
        var protector = dataProtectionProvider.CreateProtector("ProviderServiceDataProtector");
        sutProvider.GetDependency<IDataProtectionProvider>().CreateProtector("ProviderServiceDataProtector")
            .Returns(protector);
        sutProvider.Create();

        providerUser.Email = user.Email;
        var token = protector.Protect($"ProviderUserInvite {providerUser.Id} {user.Email} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}");

        var pu = await sutProvider.Sut.AcceptUserAsync(providerUser.Id, user, token);
        Assert.Null(pu.Email);
        Assert.Equal(ProviderUserStatusType.Accepted, pu.Status);
        Assert.Equal(user.Id, pu.UserId);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUsersAsync_NoValid(
        [ProviderUser(ProviderUserStatusType.Invited)] ProviderUser pu1,
        [ProviderUser(ProviderUserStatusType.Accepted)] ProviderUser pu2,
        [ProviderUser(ProviderUserStatusType.Confirmed)] ProviderUser pu3,
        SutProvider<ProviderService> sutProvider)
    {
        pu1.ProviderId = pu3.ProviderId;
        var providerUsers = new[] { pu1, pu2, pu3 };
        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        providerUserRepository.GetManyAsync(default).ReturnsForAnyArgs(providerUsers);

        var dict = providerUsers.ToDictionary(pu => pu.Id, _ => "key");
        var result = await sutProvider.Sut.ConfirmUsersAsync(pu1.ProviderId, dict, default);

        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUsersAsync_Success(
        [ProviderUser(ProviderUserStatusType.Invited)] ProviderUser pu1, User u1,
        [ProviderUser(ProviderUserStatusType.Accepted)] ProviderUser pu2, User u2,
        [ProviderUser(ProviderUserStatusType.Confirmed)] ProviderUser pu3, User u3,
        Provider provider, User user, SutProvider<ProviderService> sutProvider)
    {
        pu1.ProviderId = pu2.ProviderId = pu3.ProviderId = provider.Id;
        pu1.UserId = u1.Id;
        pu2.UserId = u2.Id;
        pu3.UserId = u3.Id;
        var providerUsers = new[] { pu1, pu2, pu3 };

        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        providerUserRepository.GetManyAsync(default).ReturnsForAnyArgs(providerUsers);
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        providerRepository.GetByIdAsync(provider.Id).Returns(provider);
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { u1, u2, u3 });

        var dict = providerUsers.ToDictionary(pu => pu.Id, _ => "key");
        var result = await sutProvider.Sut.ConfirmUsersAsync(pu1.ProviderId, dict, user.Id);

        Assert.Equal("Invalid user.", result[0].Item2);
        Assert.Equal("", result[1].Item2);
        Assert.Equal("Invalid user.", result[2].Item2);
    }

    [Theory, BitAutoData]
    public async Task SaveUserAsync_UserIdIsInvalid_Throws(ProviderUser providerUser,
        SutProvider<ProviderService> sutProvider)
    {
        providerUser.Id = default;
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveUserAsync(providerUser, default));
        Assert.Equal("Invite the user first.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SaveUserAsync_Success(
        [ProviderUser(type: ProviderUserType.ProviderAdmin)] ProviderUser providerUser, User savingUser,
        SutProvider<ProviderService> sutProvider)
    {
        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        providerUserRepository.GetByIdAsync(providerUser.Id).Returns(providerUser);

        await sutProvider.Sut.SaveUserAsync(providerUser, savingUser.Id);
        await providerUserRepository.Received().ReplaceAsync(providerUser);
        await sutProvider.GetDependency<IEventService>().Received()
            .LogProviderUserEventAsync(providerUser, EventType.ProviderUser_Updated, null);
    }

    [Theory, BitAutoData]
    public async Task DeleteUsersAsync_NoRemainingOwner_Throws(Provider provider, User deletingUser,
        ICollection<ProviderUser> providerUsers, SutProvider<ProviderService> sutProvider)
    {
        var userIds = providerUsers.Select(pu => pu.Id);

        providerUsers.First().UserId = deletingUser.Id;
        foreach (var providerUser in providerUsers)
        {
            providerUser.ProviderId = provider.Id;
        }
        providerUsers.Last().ProviderId = default;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);
        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        providerUserRepository.GetManyAsync(default).ReturnsForAnyArgs(providerUsers);
        providerUserRepository.GetManyByProviderAsync(default, default).ReturnsForAnyArgs(new ProviderUser[] { });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteUsersAsync(provider.Id, userIds, deletingUser.Id));
        Assert.Equal("Provider must have at least one confirmed ProviderAdmin.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task DeleteUsersAsync_Success(Provider provider, User deletingUser, ICollection<ProviderUser> providerUsers,
        [ProviderUser(ProviderUserStatusType.Confirmed, ProviderUserType.ProviderAdmin)] ProviderUser remainingOwner,
        SutProvider<ProviderService> sutProvider)
    {
        var userIds = providerUsers.Select(pu => pu.Id);

        providerUsers.First().UserId = deletingUser.Id;
        foreach (var providerUser in providerUsers)
        {
            providerUser.ProviderId = provider.Id;
        }
        providerUsers.Last().ProviderId = default;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);
        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        providerUserRepository.GetManyAsync(default).ReturnsForAnyArgs(providerUsers);
        providerUserRepository.GetManyByProviderAsync(default, default).ReturnsForAnyArgs(new[] { remainingOwner });

        var result = await sutProvider.Sut.DeleteUsersAsync(provider.Id, userIds, deletingUser.Id);

        Assert.NotEmpty(result);
        Assert.Equal("You cannot remove yourself.", result[0].Item2);
        Assert.Equal("", result[1].Item2);
        Assert.Equal("Invalid user.", result[2].Item2);
    }

    [Theory, BitAutoData]
    public async Task AddOrganization_OrganizationAlreadyBelongsToAProvider_Throws(Provider provider,
        Organization organization, ProviderOrganization po, User user, string key,
        SutProvider<ProviderService> sutProvider)
    {
        po.OrganizationId = organization.Id;
        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);
        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByOrganizationId(organization.Id)
            .Returns(po);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.AddOrganization(provider.Id, organization.Id, user.Id, key));
        Assert.Equal("Organization already belongs to a provider.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task AddOrganization_Success(Provider provider, Organization organization, User user, string key,
        SutProvider<ProviderService> sutProvider)
    {
        organization.PlanType = PlanType.EnterpriseAnnually;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);
        var providerOrganizationRepository = sutProvider.GetDependency<IProviderOrganizationRepository>();
        providerOrganizationRepository.GetByOrganizationId(organization.Id).ReturnsNull();
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        await sutProvider.Sut.AddOrganization(provider.Id, organization.Id, user.Id, key);

        await providerOrganizationRepository.ReceivedWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IEventService>()
            .Received().LogProviderOrganizationEventAsync(Arg.Any<ProviderOrganization>(),
                EventType.ProviderOrganization_Added);
    }

    [Theory, BitAutoData]
    public async Task AddOrganizations_Success(Provider provider, ICollection<Organization> organizations, User user, string key,
        SutProvider<ProviderService> sutProvider)
    {
        var providerOrganizationRepository = sutProvider.GetDependency<IProviderOrganizationRepository>();

        foreach (var organization in organizations)
        {
            organization.PlanType = PlanType.EnterpriseAnnually;
            providerOrganizationRepository.GetByOrganizationId(organization.Id).ReturnsNull();
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        }

        var organizationIds = organizations.Select(o => o.Id).ToArray();

        await sutProvider.Sut.AddOrganizations(provider.Id, organizationIds, user.Id, key);

        await providerOrganizationRepository.ReceivedWithAnyArgs().CreateWithManyOrganizations(Arg.Is<ProviderOrganization>(p => p.ProviderId == provider.Id), organizationIds);
        await sutProvider.GetDependency<IEventService>()
            .Received().LogProviderOrganizationEventsAsync(provider.Id, Arg.Any<IEnumerable<ProviderOrganization>>(),
                EventType.ProviderOrganization_Added);
    }

    [Theory, BitAutoData]
    public async Task CreateOrganizationAsync_Success(Provider provider, OrganizationSignup organizationSignup,
        Organization organization, string clientOwnerEmail, User user, SutProvider<ProviderService> sutProvider)
    {
        organizationSignup.Plan = PlanType.EnterpriseAnnually;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);
        var providerOrganizationRepository = sutProvider.GetDependency<IProviderOrganizationRepository>();
        sutProvider.GetDependency<IOrganizationService>().SignUpAsync(organizationSignup, true)
            .Returns(Tuple.Create(organization, null as OrganizationUser));

        var providerOrganization =
            await sutProvider.Sut.CreateOrganizationAsync(provider.Id, organizationSignup, clientOwnerEmail, user);

        await providerOrganizationRepository.ReceivedWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IEventService>()
            .Received().LogProviderOrganizationEventAsync(providerOrganization,
                EventType.ProviderOrganization_Created);
        await sutProvider.GetDependency<IOrganizationService>()
            .Received().InviteUsersAsync(organization.Id, user.Id, Arg.Is<IEnumerable<(OrganizationUserInvite, string)>>(
                t => t.Count() == 1 &&
                t.First().Item1.Emails.Count() == 1 &&
                t.First().Item1.Emails.First() == clientOwnerEmail &&
                t.First().Item1.Type == OrganizationUserType.Owner &&
                t.First().Item1.AccessAll &&
                t.First().Item2 == null));
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganization_ProviderOrganizationIsInvalid_Throws(Provider provider,
        ProviderOrganization providerOrganization, User user, SutProvider<ProviderService> sutProvider)
    {
        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);
        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByIdAsync(providerOrganization.Id)
            .ReturnsNull();

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveOrganizationAsync(provider.Id, providerOrganization.Id, user.Id));
        Assert.Equal("Invalid organization.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganization_ProviderOrganizationBelongsToWrongProvider_Throws(Provider provider,
        ProviderOrganization providerOrganization, User user, SutProvider<ProviderService> sutProvider)
    {
        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);
        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByIdAsync(providerOrganization.Id)
            .Returns(providerOrganization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveOrganizationAsync(provider.Id, providerOrganization.Id, user.Id));
        Assert.Equal("Invalid organization.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganization_HasNoOwners_Throws(Provider provider,
        ProviderOrganization providerOrganization, User user, SutProvider<ProviderService> sutProvider)
    {
        providerOrganization.ProviderId = provider.Id;
        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);
        sutProvider.GetDependency<IProviderOrganizationRepository>().GetByIdAsync(providerOrganization.Id)
            .Returns(providerOrganization);
        sutProvider.GetDependency<IOrganizationService>().HasConfirmedOwnersExceptAsync(default, default, default)
            .ReturnsForAnyArgs(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RemoveOrganizationAsync(provider.Id, providerOrganization.Id, user.Id));
        Assert.Equal("Organization needs to have at least one confirmed owner.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RemoveOrganization_Success(Provider provider,
        ProviderOrganization providerOrganization, User user, SutProvider<ProviderService> sutProvider)
    {
        providerOrganization.ProviderId = provider.Id;
        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);
        var providerOrganizationRepository = sutProvider.GetDependency<IProviderOrganizationRepository>();
        providerOrganizationRepository.GetByIdAsync(providerOrganization.Id).Returns(providerOrganization);
        sutProvider.GetDependency<IOrganizationService>().HasConfirmedOwnersExceptAsync(default, default, default)
            .ReturnsForAnyArgs(true);

        await sutProvider.Sut.RemoveOrganizationAsync(provider.Id, providerOrganization.Id, user.Id);
        await providerOrganizationRepository.Received().DeleteAsync(providerOrganization);
        await sutProvider.GetDependency<IEventService>().Received()
            .LogProviderOrganizationEventAsync(providerOrganization, EventType.ProviderOrganization_Removed);
    }
}
