#nullable enable

using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Dirt.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Bit.Core.Vault.Repositories;
using Bit.Identity.IdentityServer.ClientProviders;
using Bit.Identity.Models.Request.Accounts;
using Bit.Identity.Models.Response.Accounts;
using Bit.SharedWeb.Utilities;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Bit.Identity.Controllers;

/// <summary>
/// An internal deletion for a service of another platform that shares this install's tenants: deletes this install's
/// whole copy of providers, organizations and users, by their identifiers, once that platform has served them for longer
/// than this copy could still be needed.
/// </summary>
/// <remarks>
/// <para>
/// Only on the internal listener (<see cref="GlobalSettings.StaleCopyDeletionPort"/>), and only for a token this Identity
/// issued to a <c>release.{service}</c> client (<see cref="StaleCopyDeletionClientProvider"/>) for
/// <see cref="ApiScopes.InternalStaleCopyDeletion"/>; a token for anything else is <c>401</c>. When
/// <see cref="GlobalSettings.StaleCopyDeletionClientSecret"/> or the port is unset, or the request came in on another
/// port, the route is <c>404</c>. It is never meant to be reachable from outside the install's network: the proxy in
/// front of Identity should refuse <c>/internal/</c> as well.
/// </para>
/// <para>
/// It deletes as the organization and account deletions do, with their repositories and file stores, but not with
/// their commands: those cancel the tenant's subscription, which the other platform now bills, push a logout to the
/// person's devices, which now sign in there, and record events, and nothing here is anybody's doing. The attachment
/// files of the organizations' ciphers and of the users' own go first, then their Send files, then the rows, so a
/// request that fails part way finds what is left when repeated. Nothing is decrypted: it reads identifiers and which
/// ciphers have attachments, returns only counts and logs only the counts and the client's identifier. Safe to repeat:
/// a tenant already deleted is counted as already gone.
/// </para>
/// </remarks>
[Route("internal/stale-copies")]
// A trusted service's endpoint, not a client's: kept out of the OpenAPI the SDK is generated from.
[ApiExplorerSettings(IgnoreApi = true)]
[ExceptionHandlerFilter]
public class StaleCopyDeletionController : Controller
{
    /// <summary>The most identifiers, of all three kinds together, one request may name.</summary>
    public const int MaxIds = 100;

    private readonly IProviderRepository _providerRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICipherRepository _cipherRepository;
    private readonly IAttachmentStorageService _attachmentStorageService;
    private readonly ISendFileStorageService _sendFileStorageService;
    private readonly IOrganizationAbilityCacheService _organizationAbilityCacheService;
    private readonly ITokenValidator _tokenValidator;
    private readonly GlobalSettings _globalSettings;
    private readonly ILogger<StaleCopyDeletionController> _logger;

    public StaleCopyDeletionController(
        IProviderRepository providerRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        ICipherRepository cipherRepository,
        IAttachmentStorageService attachmentStorageService,
        ISendFileStorageService sendFileStorageService,
        IOrganizationAbilityCacheService organizationAbilityCacheService,
        ITokenValidator tokenValidator,
        GlobalSettings globalSettings,
        ILogger<StaleCopyDeletionController> logger)
    {
        _providerRepository = providerRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _cipherRepository = cipherRepository;
        _attachmentStorageService = attachmentStorageService;
        _sendFileStorageService = sendFileStorageService;
        _organizationAbilityCacheService = organizationAbilityCacheService;
        _tokenValidator = tokenValidator;
        _globalSettings = globalSettings;
        _logger = logger;
    }

    /// <summary>Delete providers', organizations' and users' whole copies</summary>
    /// <remarks>
    /// Providers first (their staff and client links), then organizations (as <c>Organization_DeleteById</c> deletes
    /// them, with their ciphers, collections, groups, members, policies, single sign-on and Secrets Manager data, and
    /// the clean-up of their event log enqueued), then users (as <c>User_DeleteById</c> deletes them, with their ciphers,
    /// folders, devices, passkeys, memberships and emergency access grants). <c>400</c> for more than
    /// <see cref="MaxIds"/> identifiers, with nothing deleted.
    /// </remarks>
    [HttpPost("delete")]
    [ProducesResponseType(typeof(StaleCopyDeletionResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync([FromBody] StaleCopyDeletionRequestModel? model)
    {
        if (!CoreHelpers.SettingHasValue(_globalSettings.StaleCopyDeletionClientSecret)
            || _globalSettings.StaleCopyDeletionPort is not { } port
            || HttpContext.Connection.LocalPort != port)
        {
            return NotFound();
        }

        var client = await AuthorizedClientAsync();
        if (client is null)
        {
            return Unauthorized();
        }

        if (model is null)
        {
            throw new BadRequestException("Name the providers, organizations and users whose copies to delete.");
        }

        var providerIds = (model.ProviderIds ?? []).Distinct().ToList();
        var organizationIds = (model.OrganizationIds ?? []).Distinct().ToList();
        var userIds = (model.UserIds ?? []).Distinct().ToList();
        if (providerIds.Count + organizationIds.Count + userIds.Count > MaxIds)
        {
            throw new BadRequestException($"At most {MaxIds} providers, organizations and users at a time.");
        }

        var (providersDeleted, organizationsDeleted, usersDeleted, alreadyGone, ciphersWithAttachments) = (0, 0, 0, 0, 0);
        foreach (var providerId in providerIds)
        {
            var provider = await _providerRepository.GetByIdAsync(providerId);
            if (provider is null)
            {
                alreadyGone++;
                continue;
            }

            await _providerRepository.DeleteAsync(provider);
            providersDeleted++;
        }

        foreach (var organizationId in organizationIds)
        {
            var organization = await _organizationRepository.GetByIdAsync(organizationId);
            if (organization is null)
            {
                alreadyGone++;
                continue;
            }

            // As OrganizationDeleteCommand, less the subscription's cancellation and its Key Connector refusal, which
            // keeps members able to unlock when a customer deletes their organization; the organization and its members
            // live on the other platform now: the files, the rows with the event log's clean-up, the cached ability.
            ciphersWithAttachments += await DeleteAttachmentFilesAsync(
                await _cipherRepository.GetManyByOrganizationIdAsync(organizationId));
            await _sendFileStorageService.DeleteFilesForOrganizationAsync(organizationId);
            await _organizationRepository.DeleteAndCreateDeleteTasksAsync(organization, [OrganizationDeleteTaskType.EventsCleanup]);
            await _organizationAbilityCacheService.DeleteOrganizationAbilityAsync(organizationId);
            organizationsDeleted++;
        }

        foreach (var userId in userIds)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user is null)
            {
                alreadyGone++;
                continue;
            }

            // As UserService.DeleteAsync, less the subscription's cancellation and the logout push. Its attachment files
            // go too, which the account deletion leaves: IAttachmentStorageService.DeleteAttachmentsForUserAsync deletes
            // nothing on Azure or local storage, so they are deleted cipher by cipher, as an organization's are.
            ciphersWithAttachments += await DeleteAttachmentFilesAsync(
                await _cipherRepository.GetManyByUserIdAsync(userId, withOrganizations: false));
            await _sendFileStorageService.DeleteFilesForUserAsync(userId);
            await _userRepository.DeleteAsync(user);
            usersDeleted++;
        }

        _logger.LogInformation(
            "Deleted stale copies at internal client {ClientId}'s request: {ProvidersDeleted} providers, "
            + "{OrganizationsDeleted} organizations and {UsersDeleted} users deleted, {AlreadyGone} already gone, "
            + "attachment files of {CiphersWithAttachmentsDeleted} ciphers deleted.",
            client, providersDeleted, organizationsDeleted, usersDeleted, alreadyGone, ciphersWithAttachments);

        return Ok(new StaleCopyDeletionResponseModel(
            providersDeleted, organizationsDeleted, usersDeleted, alreadyGone, ciphersWithAttachments));
    }

    /// <summary>
    /// Deletes the attachment files of each cipher that has any, as <c>CipherService.DeleteAttachmentsForOrganizationAsync</c>
    /// does, and answers how many ciphers that was. Only which ciphers have attachments is read: no file, key or name.
    /// </summary>
    private async Task<int> DeleteAttachmentFilesAsync(IEnumerable<Core.Vault.Entities.Cipher> ciphers)
    {
        var deleted = 0;
        foreach (var cipher in ciphers.Where(cipher => cipher.GetAttachments()?.Count > 0))
        {
            await _attachmentStorageService.DeleteAttachmentsForCipherAsync(cipher.Id);
            deleted++;
        }

        return deleted;
    }

    /// <summary>
    /// The client a bearer token this Identity issued, still valid, for the deletion's scope, names, when it is one of
    /// the deletion's clients; otherwise null.
    /// </summary>
    private async Task<string?> AuthorizedClientAsync()
    {
        var authorization = Request.Headers[HeaderNames.Authorization].ToString();
        const string bearer = "Bearer ";
        if (!authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorization[bearer.Length..].Trim();
        if (token.Length == 0)
        {
            return null;
        }

        var validated = await _tokenValidator.ValidateAccessTokenAsync(token, ApiScopes.InternalStaleCopyDeletion);
        if (validated.IsError)
        {
            return null;
        }

        var client = validated.Claims?.FirstOrDefault(claim => claim.Type == JwtClaimTypes.ClientId)?.Value;
        return client is not null && client.StartsWith(StaleCopyDeletionClientProvider.Prefix + ".", StringComparison.Ordinal)
            ? client
            : null;
    }
}
