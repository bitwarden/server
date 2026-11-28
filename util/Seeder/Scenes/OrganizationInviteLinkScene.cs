using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public struct OrganizationInviteLinkSceneResult
{
    public Guid InviteLinkId { get; init; }
    public string Code { get; init; }
    /// <summary>
    /// <c>#/join/{organizationId}/{code}?key={secret}</c>, for callers that build the URL from their own web vault
    /// base address.
    /// </summary>
    public string InviteUrlFragment { get; init; }
    /// <summary>
    /// The full invite URL on the configured web vault (<c>globalSettings.baseServiceUri.vault</c>).
    /// </summary>
    public string InviteUrl { get; init; }
}

/// <summary>
/// Creates an invite link for an existing organization and returns the full, working invite URL. The sealed invite
/// and its secret are made with the same SDK code the clients use, so a new user whose email is in an allowed domain
/// can open the URL and join. The secret only ever exists in the returned URL; the server stores the sealed invite.
/// </summary>
public class OrganizationInviteLinkScene(
    IOrganizationRepository organizationRepository,
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IGlobalSettings globalSettings,
    IManglerService manglerService) : IScene<OrganizationInviteLinkScene.Request, OrganizationInviteLinkSceneResult>
{
    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        /// <summary>
        /// The base64 organization key, as returned by <c>SingleOrganizationScene</c>.
        /// </summary>
        [Required]
        public required string OrganizationKeyB64 { get; set; }
        [Required]
        public required List<string> AllowedDomains { get; set; }
        /// <summary>
        /// Auto-confirmation through the link. The server does not support it yet, so it defaults to false.
        /// </summary>
        public bool SupportsConfirmation { get; set; }
    }

    public async Task<SceneResult<OrganizationInviteLinkSceneResult>> SeedAsync(Request request)
    {
        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            throw new InvalidOperationException($"Organization {request.OrganizationId} not found.");
        }

        if (!organization.UseInviteLinks)
        {
            throw new InvalidOperationException(
                $"Organization {request.OrganizationId} cannot use invite links; seed it on a plan that has them (Enterprise) or set Overrides.UseInviteLinks.");
        }

        if (string.IsNullOrEmpty(organization.PrivateKey))
        {
            throw new InvalidOperationException(
                $"Organization {request.OrganizationId} has no private key; cannot bind an invite to it.");
        }

        var allowedDomains = OrganizationInviteLinkSeeder.SanitizeDomains(request.AllowedDomains);
        if (allowedDomains.Count == 0)
        {
            throw new InvalidOperationException("At least one allowed domain is required.");
        }

        if (await organizationInviteLinkRepository.GetByOrganizationIdAsync(request.OrganizationId) != null)
        {
            throw new InvalidOperationException($"Organization {request.OrganizationId} already has an invite link.");
        }

        var invite = RustSdkService.MakeOrganizationInvite(
            request.OrganizationKeyB64, organization.PrivateKey, request.SupportsConfirmation);

        var inviteLink = OrganizationInviteLinkSeeder.Create(
            organization.Id, allowedDomains, invite.Invite, request.SupportsConfirmation);
        await organizationInviteLinkRepository.CreateAsync(inviteLink);

        var fragment = $"#/join/{organization.Id}/{inviteLink.Code}?key={invite.Secret}";

        return new SceneResult<OrganizationInviteLinkSceneResult>(
            result: new OrganizationInviteLinkSceneResult
            {
                InviteLinkId = inviteLink.Id,
                Code = inviteLink.Code,
                InviteUrlFragment = fragment,
                InviteUrl = $"{globalSettings.BaseServiceUri.Vault?.TrimEnd('/')}/{fragment}"
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
