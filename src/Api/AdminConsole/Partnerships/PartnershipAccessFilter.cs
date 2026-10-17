using Bit.Core;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bit.Api.AdminConsole.Partnerships;

/// <summary>
/// Admits a partner API request only when the authenticated organization has an active partnership, then exposes that
/// partnership through <see cref="IPartnershipRequestContext"/>.
/// </summary>
/// <remarks>
/// Returns 404 when the feature is off or the organization has no partnership, without saying which, and 403 when the
/// partnership is inactive.
/// </remarks>
public sealed class PartnershipAccessFilter(
    ICurrentContext currentContext,
    IFeatureService featureService,
    IOrganizationPartnershipRepository organizationPartnershipRepository,
    PartnershipRequestContext partnershipRequestContext) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var partnership = featureService.IsEnabled(FeatureFlagKeys.PartnerSponsorships) &&
                          currentContext.OrganizationId is { } organizationId
            ? await organizationPartnershipRepository.GetByOrganizationIdAsync(organizationId)
            : null;

        if (partnership is null)
        {
            context.Result = PartnershipErrorResults.FromError(new PartnershipNotFound(), context.HttpContext);
            return;
        }

        if (partnership.Status != PartnershipStatus.Active)
        {
            context.Result = PartnershipErrorResults.PartnershipNotActive(context.HttpContext);
            return;
        }

        partnershipRequestContext.SetPartnership(partnership);
        await next();
    }
}
