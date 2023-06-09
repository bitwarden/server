using System.Text.Json;
using Bit.Core.Auth.Models.Business;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers;

public class InviteOrganizationUserCommand : OrganizationUserCommand, IInviteOrganizationUserCommand
{
    private readonly IEventService _eventService;
    private readonly IDataProtector _dataProtector;
    private readonly IMailService _mailService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IGlobalSettings _globalSettings;

    public InviteOrganizationUserCommand(
        ICurrentContext currentContext,
        IEventService eventService,
        IDataProtectionProvider dataProtectionProvider,
        IMailService mailService,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService,
        IReferenceEventService referenceEventService,
        IGlobalSettings globalSettings)
        : base(currentContext, organizationRepository)
    {
        _eventService = eventService;
        _dataProtector = dataProtectionProvider.CreateProtector("InviteOrganizationUserCommandDataProtector");
        _mailService = mailService;
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
        _referenceEventService = referenceEventService;
        _globalSettings = globalSettings;
    }

    public async Task<List<OrganizationUser>> InviteUsersAsync(Guid organizationId, Guid? invitingUserId,
        IEnumerable<(OrganizationUserInvite invite, string externalId)> invites)
    {
        var inviteTypes = new HashSet<OrganizationUserType>(invites.Where(i => i.invite.Type.HasValue)
            .Select(i => i.invite.Type.Value));
        if (invitingUserId.HasValue && inviteTypes.Count > 0)
        {
            foreach (var (invite, _) in invites)
            {
                await ValidateOrganizationUserUpdatePermissions(organizationId, invite.Type.Value, null, invite.Permissions);
                await ValidateOrganizationCustomPermissionsEnabledAsync(organizationId, invite.Type.Value);
            }
        }

        var (organizationUsers, events) = await SaveUsersSendInvitesAsync(organizationId, invites, systemUser: null);

        await _eventService.LogOrganizationUserEventsAsync(events);

        return organizationUsers;
    }

    public async Task<List<OrganizationUser>> InviteUsersAsync(Guid organizationId, EventSystemUser systemUser,
        IEnumerable<(OrganizationUserInvite invite, string externalId)> invites)
    {
        var (organizationUsers, events) = await SaveUsersSendInvitesAsync(organizationId, invites, systemUser);

        await _eventService.LogOrganizationUserEventsAsync(events.Select(e => (e.Item1, e.Item2, systemUser, e.Item3)));

        return organizationUsers;
    }

    public async Task<OrganizationUser> InviteUserAsync(Guid organizationId, Guid? invitingUserId, string email,
        OrganizationUserType type, bool accessAll, string externalId, IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups)
    {
        return await SaveUserSendInviteAsync(organizationId, invitingUserId, systemUser: null, email, type, accessAll, externalId, collections, groups);
    }

    public async Task<OrganizationUser> InviteUserAsync(Guid organizationId, EventSystemUser systemUser, string email,
        OrganizationUserType type, bool accessAll, string externalId, IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups)
    {
        return await SaveUserSendInviteAsync(organizationId, invitingUserId: null, systemUser, email, type, accessAll, externalId, collections, groups);
    }

    private async Task<(List<OrganizationUser> organizationUsers, List<(OrganizationUser, EventType, DateTime?)> events)> SaveUsersSendInvitesAsync(Guid organizationId,
        IEnumerable<(OrganizationUserInvite invite, string externalId)> invites, EventSystemUser? systemUser)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        var initialSeatCount = organization.Seats;
        if (organization == null || invites.Any(i => i.invite.Emails == null))
        {
            throw new NotFoundException();
        }

        var newSeatsRequired = 0;
        var existingEmails = new HashSet<string>(await _organizationUserRepository.SelectKnownEmailsAsync(
            organizationId, invites.SelectMany(i => i.invite.Emails), false), StringComparer.InvariantCultureIgnoreCase);
        if (organization.Seats.HasValue)
        {
            var occupiedSeats = await _organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
            var availableSeats = organization.Seats.Value - occupiedSeats;
            newSeatsRequired = invites.Sum(i => i.invite.Emails.Count()) - existingEmails.Count() - availableSeats;
        }

        if (newSeatsRequired > 0)
        {
            var (canScale, failureReason) = CanScale(organization, newSeatsRequired);
            if (!canScale)
            {
                throw new BadRequestException(failureReason);
            }
        }

        var invitedAreAllOwners = invites.All(i => i.invite.Type == OrganizationUserType.Owner);
        if (!invitedAreAllOwners && !await _organizationService.HasConfirmedOwnersExceptAsync(organizationId, new Guid[] { }, includeProvider: true))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        var orgUsers = new List<OrganizationUser>();
        var limitedCollectionOrgUsers = new List<(OrganizationUser, IEnumerable<CollectionAccessSelection>)>();
        var orgUserGroups = new List<(OrganizationUser, IEnumerable<Guid>)>();
        var orgUserInvitedCount = 0;
        var exceptions = new List<Exception>();
        var events = new List<(OrganizationUser, EventType, DateTime?)>();
        foreach (var (invite, externalId) in invites)
        {
            // Prevent duplicate invitations
            foreach (var email in invite.Emails.Distinct())
            {
                try
                {
                    // Make sure user is not already invited
                    if (existingEmails.Contains(email))
                    {
                        continue;
                    }

                    var orgUser = new OrganizationUser
                    {
                        OrganizationId = organizationId,
                        UserId = null,
                        Email = email.ToLowerInvariant(),
                        Key = null,
                        Type = invite.Type.Value,
                        Status = OrganizationUserStatusType.Invited,
                        AccessAll = invite.AccessAll,
                        AccessSecretsManager = invite.AccessSecretsManager,
                        ExternalId = externalId,
                        CreationDate = DateTime.UtcNow,
                        RevisionDate = DateTime.UtcNow,
                    };

                    if (invite.Permissions != null)
                    {
                        orgUser.Permissions = JsonSerializer.Serialize(invite.Permissions, JsonHelpers.CamelCase);
                    }

                    if (!orgUser.AccessAll && invite.Collections.Any())
                    {
                        limitedCollectionOrgUsers.Add((orgUser, invite.Collections));
                    }
                    else
                    {
                        orgUsers.Add(orgUser);
                    }

                    if (invite.Groups != null && invite.Groups.Any())
                    {
                        orgUserGroups.Add((orgUser, invite.Groups));
                    }

                    events.Add((orgUser, EventType.OrganizationUser_Invited, DateTime.UtcNow));
                    orgUserInvitedCount++;
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }
        }

        if (exceptions.Any())
        {
            throw new AggregateException("One or more errors occurred while inviting users.", exceptions);
        }

        var prorationDate = DateTime.UtcNow;
        try
        {
            await _organizationUserRepository.CreateManyAsync(orgUsers);
            foreach (var (orgUser, collections) in limitedCollectionOrgUsers)
            {
                await _organizationUserRepository.CreateAsync(orgUser, collections);
            }

            foreach (var (orgUser, groups) in orgUserGroups)
            {
                await _organizationUserRepository.UpdateGroupsAsync(orgUser.Id, groups);
            }

            if (!await _currentContext.ManageUsers(organization.Id))
            {
                throw new BadRequestException("Cannot add seats. Cannot manage organization users.");
            }

            await _organizationService.AutoAddSeatsAsync(organization, newSeatsRequired, prorationDate);
            await SendInvitesAsync(orgUsers.Concat(limitedCollectionOrgUsers.Select(u => u.Item1)), organization);

            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.InvitedUsers, organization, _currentContext)
                {
                    Users = orgUserInvitedCount
                });
        }
        catch (Exception e)
        {
            // Revert any added users.
            var invitedOrgUserIds = orgUsers.Select(u => u.Id).Concat(limitedCollectionOrgUsers.Select(u => u.Item1.Id));
            await _organizationUserRepository.DeleteManyAsync(invitedOrgUserIds);
            var currentSeatCount = (await _organizationRepository.GetByIdAsync(organization.Id)).Seats;

            if (initialSeatCount.HasValue && currentSeatCount.HasValue && currentSeatCount.Value != initialSeatCount.Value)
            {
                await _organizationService.AdjustSeatsAsync(organization.Id, initialSeatCount.Value - currentSeatCount.Value, prorationDate);
            }

            exceptions.Add(e);
        }

        if (exceptions.Any())
        {
            throw new AggregateException("One or more errors occurred while inviting users.", exceptions);
        }

        return (orgUsers, events);
    }

    private async Task<OrganizationUser> SaveUserSendInviteAsync(Guid organizationId, Guid? invitingUserId, EventSystemUser? systemUser, string email,
        OrganizationUserType type, bool accessAll, string externalId, IEnumerable<CollectionAccessSelection> collections, IEnumerable<Guid> groups)
    {
        var invite = new OrganizationUserInvite()
        {
            Emails = new List<string> { email },
            Type = type,
            AccessAll = accessAll,
            Collections = collections,
            Groups = groups
        };
        var results = systemUser.HasValue ? await InviteUsersAsync(organizationId, systemUser.Value,
            new (OrganizationUserInvite, string)[] { (invite, externalId) }) : await InviteUsersAsync(organizationId, invitingUserId,
            new (OrganizationUserInvite, string)[] { (invite, externalId) });
        var result = results.FirstOrDefault();
        if (result == null)
        {
            throw new BadRequestException("This user has already been invited.");
        }
        return result;
    }

    private async Task SendInvitesAsync(IEnumerable<OrganizationUser> orgUsers, Organization organization)
    {
        string MakeToken(OrganizationUser orgUser) =>
            _dataProtector.Protect($"OrganizationUserInvite {orgUser.Id} {orgUser.Email} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}");

        await _mailService.BulkSendOrganizationInviteEmailAsync(organization.Name,
            orgUsers.Select(o => (o, new ExpiringToken(MakeToken(o), DateTime.UtcNow.AddDays(5)))), organization.PlanType == PlanType.Free);
    }

    private (bool canScale, string failureReason) CanScale(Organization organization,
        int seatsToAdd)
    {
        var failureReason = "";
        if (_globalSettings.SelfHosted)
        {
            failureReason = "Cannot autoscale on self-hosted instance.";
            return (false, failureReason);
        }

        if (seatsToAdd < 1)
        {
            return (true, failureReason);
        }

        if (organization.Seats.HasValue &&
            organization.MaxAutoscaleSeats.HasValue &&
            organization.MaxAutoscaleSeats.Value < organization.Seats.Value + seatsToAdd)
        {
            return (false, $"Seat limit has been reached.");
        }

        return (true, failureReason);
    }
}
