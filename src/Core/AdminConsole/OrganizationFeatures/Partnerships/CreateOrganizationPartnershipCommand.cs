using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public class CreateOrganizationPartnershipCommand(
    IOrganizationRepository organizationRepository,
    IOrganizationPartnershipRepository organizationPartnershipRepository,
    TimeProvider timeProvider)
    : ICreateOrganizationPartnershipCommand
{
    public async Task<CommandResult<OrganizationPartnership>> CreateAsync(CreateOrganizationPartnershipRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > CreateOrganizationPartnershipRequest.NameMaxLength)
        {
            return new InvalidPartnershipName();
        }

        if (request.BindingMode != PartnershipBindingMode.Token)
        {
            return new UnsupportedBindingMode();
        }

        var origins = new List<string>();
        foreach (var origin in request.RegisteredReturnOrigins)
        {
            if (!TryNormalizeOrigin(origin, out var normalized))
            {
                return new InvalidReturnOrigin();
            }
            origins.Add(normalized);
        }

        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization is null)
        {
            return new OrganizationNotFound();
        }

        if (await organizationPartnershipRepository.GetByOrganizationIdAsync(request.OrganizationId) is not null)
        {
            return new PartnershipAlreadyExists();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var partnership = new OrganizationPartnership
        {
            OrganizationId = request.OrganizationId,
            Name = request.Name,
            Status = PartnershipStatus.Active,
            SponsoredPlanType = request.SponsoredPlanType,
            BindingMode = request.BindingMode,
            CreationDate = now,
            RevisionDate = now,
        };
        partnership.SetRegisteredReturnOrigins(origins.Distinct(StringComparer.OrdinalIgnoreCase));
        partnership.SetNewId();

        await organizationPartnershipRepository.CreateAsync(partnership);

        return partnership;
    }

    /// <summary>
    /// Accepts only a canonical https origin: scheme, host, and an optional non-default port.
    /// </summary>
    private static bool TryNormalizeOrigin(string? origin, out string normalized)
    {
        normalized = string.Empty;
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        normalized = uri.GetLeftPart(UriPartial.Authority);
        return string.Equals(origin, normalized, StringComparison.OrdinalIgnoreCase);
    }
}
