#nullable enable
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Auth.UserFeatures.Registration.Implementations;

/// <summary>
/// If email verification is enabled, this command will send a verification email to the user which will
///  contain a link to complete the registration process.
/// If email verification is disabled, this command will return a token that can be used to complete the registration process directly.
/// </summary>
public class SendVerificationEmailForRegistrationCommand : ISendVerificationEmailForRegistrationCommand
{
    private readonly ILogger<SendVerificationEmailForRegistrationCommand> _logger;
    private readonly IUserRepository _userRepository;
    private readonly GlobalSettings _globalSettings;
    private readonly IMailService _mailService;
    private readonly IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> _tokenDataFactory;
    private readonly IOrganizationDomainRepository _organizationDomainRepository;
    private readonly IValidateOrganizationInviteLinkQuery _validateOrganizationInviteLinkQuery;

    public SendVerificationEmailForRegistrationCommand(
        ILogger<SendVerificationEmailForRegistrationCommand> logger,
        IUserRepository userRepository,
        GlobalSettings globalSettings,
        IMailService mailService,
        IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable> tokenDataFactory,
        IOrganizationDomainRepository organizationDomainRepository,
        IValidateOrganizationInviteLinkQuery validateOrganizationInviteLinkQuery)
    {
        _logger = logger;
        _userRepository = userRepository;
        _globalSettings = globalSettings;
        _mailService = mailService;
        _tokenDataFactory = tokenDataFactory;
        _organizationDomainRepository = organizationDomainRepository;
        _validateOrganizationInviteLinkQuery = validateOrganizationInviteLinkQuery;
    }

    public async Task<string?> Run(string email, string? name, bool receiveMarketingEmails, string? fromMarketing,
        RegisterStartOpenOrgInviteRequestModel? openOrgInvite = null)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentNullException(nameof(email));
        }

        var emailDomain = EmailValidation.GetDomain(email);

        // When an open-org-invite payload is present, validate it and use its org as the
        // exclusion target for the claimed-domain block check so a user reaching registration
        // via that org's link can proceed with a domain the org has claimed.
        Guid? excludeOrganizationId = null;
        if (openOrgInvite is not null)
        {
            var validationResult = await _validateOrganizationInviteLinkQuery.ValidateAsync(
                openOrgInvite.OrganizationId, openOrgInvite.Code, email);
            if (validationResult.IsError)
            {
                throw new BadRequestException("Invalid or expired organization invite link.");
            }
            excludeOrganizationId = openOrgInvite.OrganizationId;
        }

        // DisableUserRegistration targets open self-registration. A validated open-org invite
        // is the authorization for that path, so the toggle must not block it.
        if (openOrgInvite is null && _globalSettings.DisableUserRegistration)
        {
            throw new BadRequestException("Open registration has been disabled by the system administrator.");
        }

        if (await _organizationDomainRepository.HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(
                emailDomain, excludeOrganizationId))
        {
            _logger.LogInformation(
                "User registration email verification blocked by domain claim policy. Domain: {Domain}, ExcludedOrgId: {ExcludedOrgId}",
                emailDomain, excludeOrganizationId);
            throw new BadRequestException("This email address is claimed by an organization using Bitwarden.");
        }

        // Check to see if the user already exists
        var user = await _userRepository.GetByEmailAsync(email);
        var userExists = user != null;

        if (!_globalSettings.EnableEmailVerification)
        {
            if (userExists)
            {
                throw new BadRequestException($"Email {email} is already taken");
            }

            // if user doesn't exist, return a EmailVerificationTokenable in the response body.
            var token = GenerateToken(email, name, receiveMarketingEmails);

            return token;
        }

        if (!userExists)
        {
            // If the user doesn't exist, create a new EmailVerificationTokenable and send the user
            // an email with a link to verify their email address
            var token = GenerateToken(email, name, receiveMarketingEmails);
            await _mailService.SendRegistrationVerificationEmailAsync(
                email, token, fromMarketing, openOrgInvite?.SealedOpenOrgInviteData);
        }

        // User exists but we will return a 200 regardless of whether the email was sent or not; so return null
        return null;
    }

    private string GenerateToken(string email, string? name, bool receiveMarketingEmails)
    {
        var registrationEmailVerificationTokenable = new RegistrationEmailVerificationTokenable(email, name, receiveMarketingEmails);
        return _tokenDataFactory.Protect(registrationEmailVerificationTokenable);
    }
}
