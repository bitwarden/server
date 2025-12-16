using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations.OrganizationSignUp;

[SutProviderCustomize]
public class ResellerClientOrganizationSignUpCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task SignUpResellerClientAsync_WithValidParameters_CreatesOrganizationSuccessfully(
        Organization organization,
        string ownerEmail,
        SutProvider<ResellerClientOrganizationSignUpCommand> sutProvider)
    {
        var result = await sutProvider.Sut.SignUpResellerClientAsync(organization, ownerEmail);

        Assert.NotNull(result.Organization);
        Assert.False(result.Organization.Enabled);
        Assert.Equal(OrganizationStatusType.Pending, result.Organization.Status);
        Assert.NotNull(result.OwnerOrganizationUser);
        Assert.Equal(ownerEmail, result.OwnerOrganizationUser.Email);
        Assert.Equal(OrganizationUserType.Owner, result.OwnerOrganizationUser.Type);
        Assert.Equal(OrganizationUserStatusType.Invited, result.OwnerOrganizationUser.Status);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .CreateAsync(
                Arg.Is<Organization>(o =>
                    o.Id != default &&
                    o.Name == organization.Name &&
                    o.Enabled == false &&
                    o.Status == OrganizationStatusType.Pending
                )
            );
        await sutProvider.GetDependency<IOrganizationApiKeyRepository>()
            .Received(1)
            .CreateAsync(
                Arg.Is<OrganizationApiKey>(k =>
                    k.OrganizationId == result.Organization.Id &&
                    k.Type == OrganizationApiKeyType.Default &&
                    !string.IsNullOrEmpty(k.ApiKey)
                )
            );
        await sutProvider.GetDependency<IApplicationCacheService>()
            .Received(1)
            .UpsertOrganizationAbilityAsync(Arg.Is<Organization>(o => o.Id == result.Organization.Id));
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .CreateAsync(
                Arg.Is<OrganizationUser>(u =>
                    u.OrganizationId == result.Organization.Id &&
                    u.Email == ownerEmail &&
                    u.Type == OrganizationUserType.Owner &&
                    u.Status == OrganizationUserStatusType.Invited &&
                    u.UserId == null
                )
            );
        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .Received(1)
            .SendInvitesAsync(
                Arg.Is<SendInvitesRequest>(r =>
                    r.Users.Count() == 1 &&
                    r.Users.First().Email == ownerEmail &&
                    r.Organization.Id == result.Organization.Id &&
                    r.InitOrganization == true
                )
            );
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(
                Arg.Is<OrganizationUser>(u => u.Email == ownerEmail),
                EventType.OrganizationUser_Invited
            );
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpResellerClientAsync_WhenOrganizationRepositoryThrows_PerformsCleanup(
        Organization organization,
        string ownerEmail,
        SutProvider<ResellerClientOrganizationSignUpCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>()
            .When(x => x.CreateAsync(Arg.Any<Organization>()))
            .Do(_ => throw new Exception());

        await Assert.ThrowsAsync<Exception>(
            () => sutProvider.Sut.SignUpResellerClientAsync(organization, ownerEmail));

        await AssertCleanupIsPerformed(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpResellerClientAsync_WhenOrganizationUserCreationFails_PerformsCleanup(
        Organization organization,
        string ownerEmail,
        SutProvider<ResellerClientOrganizationSignUpCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .When(x => x.CreateAsync(Arg.Any<OrganizationUser>()))
            .Do(_ => throw new Exception());

        await Assert.ThrowsAsync<Exception>(
            () => sutProvider.Sut.SignUpResellerClientAsync(organization, ownerEmail));

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .CreateAsync(Arg.Any<Organization>());
        await AssertCleanupIsPerformed(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpResellerClientAsync_WhenInvitationSendingFails_PerformsCleanup(
        Organization organization,
        string ownerEmail,
        SutProvider<ResellerClientOrganizationSignUpCommand> sutProvider)
    {
        sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .When(x => x.SendInvitesAsync(Arg.Any<SendInvitesRequest>()))
            .Do(_ => throw new Exception());

        await Assert.ThrowsAsync<Exception>(
            () => sutProvider.Sut.SignUpResellerClientAsync(organization, ownerEmail));

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .CreateAsync(Arg.Any<Organization>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .CreateAsync(Arg.Any<OrganizationUser>());
        await AssertCleanupIsPerformed(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task SignUpResellerClientAsync_WhenEventLoggingFails_PerformsCleanup(
        Organization organization,
        string ownerEmail,
        SutProvider<ResellerClientOrganizationSignUpCommand> sutProvider)
    {
        sutProvider.GetDependency<IEventService>()
            .When(x => x.LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>()))
            .Do(_ => throw new Exception());

        await Assert.ThrowsAsync<Exception>(
            () => sutProvider.Sut.SignUpResellerClientAsync(organization, ownerEmail));

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .CreateAsync(Arg.Any<Organization>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .CreateAsync(Arg.Any<OrganizationUser>());
        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .Received(1)
            .SendInvitesAsync(Arg.Any<SendInvitesRequest>());
        await AssertCleanupIsPerformed(sutProvider);
    }

    private static async Task AssertCleanupIsPerformed(SutProvider<ResellerClientOrganizationSignUpCommand> sutProvider)
    {
        await sutProvider.GetDependency<IStripePaymentService>()
            .Received(1)
            .CancelAndRecoverChargesAsync(Arg.Any<Organization>());
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .DeleteAsync(Arg.Any<Organization>());
        await sutProvider.GetDependency<IApplicationCacheService>()
            .Received(1)
            .DeleteOrganizationAbilityAsync(Arg.Any<Guid>());
    }
}
