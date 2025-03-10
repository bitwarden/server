using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Context;
using Bit.Scim.Models;
using Bit.Scim.Users.Interfaces;

namespace Bit.Scim.Users;

public class PostUserCommand : IPostUserCommand
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IPaymentService _paymentService;
    private readonly IScimContext _scimContext;

    public PostUserCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService,
        IPaymentService paymentService,
        IScimContext scimContext)
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
        _paymentService = paymentService;
        _scimContext = scimContext;
    }

    public async Task<OrganizationUserUserDetails> PostUserAsync(Guid organizationId, ScimUserRequestModel model)
    {
        var scimProvider = _scimContext.RequestScimProvider;
        var invite = model.ToOrganizationUserInvite(scimProvider);

        var email = invite.Emails.Single();
        var externalId = model.ExternalIdForInvite();

        if (string.IsNullOrWhiteSpace(email) || !model.Active)
        {
            throw new BadRequestException();
        }

        var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
        var orgUserByEmail = orgUsers.FirstOrDefault(ou => ou.Email?.ToLowerInvariant() == email);
        if (orgUserByEmail != null)
        {
            throw new ConflictException();
        }

        var orgUserByExternalId = orgUsers.FirstOrDefault(ou => ou.ExternalId == externalId);
        if (orgUserByExternalId != null)
        {
            throw new ConflictException();
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        var hasStandaloneSecretsManager = await _paymentService.HasSecretsManagerStandalone(organization);
        invite.AccessSecretsManager = hasStandaloneSecretsManager;

        var invitedOrgUser = await _organizationService.InviteUserAsync(organizationId, invitingUserId: null, EventSystemUser.SCIM,
            invite, externalId);
        var orgUser = await _organizationUserRepository.GetDetailsByIdAsync(invitedOrgUser.Id);

        return orgUser;
    }
}
