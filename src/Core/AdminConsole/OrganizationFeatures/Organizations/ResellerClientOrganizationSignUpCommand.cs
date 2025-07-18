using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public record ResellerClientOrganizationSignUpResponse(
    Organization Organization,
    OrganizationUser OwnerOrganizationUser);

/// <summary>
/// Command for signing up reseller client organizations in a pending state.
/// </summary>
public interface IResellerClientOrganizationSignUpCommand
{
    /// <summary>
    /// Sign up a reseller client organization. The organization will be created in a pending state 
    /// (disabled and with Pending status) and the owner will be invited via email. The organization 
    /// will become active once the owner accepts the invitation.
    /// </summary>
    /// <param name="organization">The organization to create.</param>
    /// <param name="ownerEmail">The email of the organization owner who will be invited.</param>
    /// <returns>A response containing the created pending organization and invited owner user.</returns>
    Task<ResellerClientOrganizationSignUpResponse> SignUpResellerClientAsync(
        Organization organization,
        string ownerEmail);
}

public class ResellerClientOrganizationSignUpCommand : IResellerClientOrganizationSignUpCommand
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IEventService _eventService;
    private readonly ISendOrganizationInvitesCommand _sendOrganizationInvitesCommand;
    private readonly IPaymentService _paymentService;

    public ResellerClientOrganizationSignUpCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationApiKeyRepository organizationApiKeyRepository,
        IApplicationCacheService applicationCacheService,
        IOrganizationUserRepository organizationUserRepository,
        IEventService eventService,
        ISendOrganizationInvitesCommand sendOrganizationInvitesCommand,
        IPaymentService paymentService)
    {
        _organizationRepository = organizationRepository;
        _organizationApiKeyRepository = organizationApiKeyRepository;
        _applicationCacheService = applicationCacheService;
        _organizationUserRepository = organizationUserRepository;
        _eventService = eventService;
        _sendOrganizationInvitesCommand = sendOrganizationInvitesCommand;
        _paymentService = paymentService;
    }

    public async Task<ResellerClientOrganizationSignUpResponse> SignUpResellerClientAsync(
        Organization organization,
        string ownerEmail)
    {
        try
        {
            var createdOrganization = await CreateOrganizationAsync(organization);
            var ownerOrganizationUser = await CreateAndInviteOwnerAsync(createdOrganization, ownerEmail);

            await _eventService.LogOrganizationUserEventAsync(ownerOrganizationUser, EventType.OrganizationUser_Invited);

            return new ResellerClientOrganizationSignUpResponse(organization, ownerOrganizationUser);
        }
        catch
        {
            await _paymentService.CancelAndRecoverChargesAsync(organization);

            if (organization.Id != default)
            {
                // Deletes the organization and all related data, including its owner user
                await _organizationRepository.DeleteAsync(organization);
                await _applicationCacheService.DeleteOrganizationAbilityAsync(organization.Id);
            }

            throw;
        }
    }

    private async Task<Organization> CreateOrganizationAsync(Organization organization)
    {
        organization.Id = CoreHelpers.GenerateComb();
        organization.Enabled = false;
        organization.Status = OrganizationStatusType.Pending;

        await _organizationRepository.CreateAsync(organization);
        await _organizationApiKeyRepository.CreateAsync(new OrganizationApiKey
        {
            OrganizationId = organization.Id,
            ApiKey = CoreHelpers.SecureRandomString(30),
            Type = OrganizationApiKeyType.Default,
            RevisionDate = DateTime.UtcNow,
        });
        await _applicationCacheService.UpsertOrganizationAbilityAsync(organization);

        return organization;
    }

    private async Task<OrganizationUser> CreateAndInviteOwnerAsync(Organization organization, string ownerEmail)
    {
        var ownerOrganizationUser = new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = null,
            Email = ownerEmail,
            Key = null,
            Type = OrganizationUserType.Owner,
            Status = OrganizationUserStatusType.Invited,
        };

        await _organizationUserRepository.CreateAsync(ownerOrganizationUser);

        await _sendOrganizationInvitesCommand.SendInvitesAsync(new SendInvitesRequest(
            users: [ownerOrganizationUser],
            organization: organization,
            initOrganization: true));

        return ownerOrganizationUser;
    }
}
