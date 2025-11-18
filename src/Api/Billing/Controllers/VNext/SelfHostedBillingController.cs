using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Api.Billing.Attributes;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.Api.Billing.Controllers.VNext;

[Authorize("Application")]
[Route("organizations/{organizationId:guid}/billing/vnext/self-host")]
[SelfHosted(SelfHostedOnly = true)]
public class SelfHostedBillingController(
    IGetOrganizationMetadataQuery getOrganizationMetadataQuery) : BaseBillingController
{
    [Authorize<MemberOrProviderRequirement>]
    [HttpGet("metadata")]
    [RequireFeature(FeatureFlagKeys.PM25379_UseNewOrganizationMetadataStructure)]
    [InjectOrganization]
    public async Task<IResult> GetMetadataAsync([BindNever] Organization organization)
    {
        var metadata = await getOrganizationMetadataQuery.Run(organization);

        if (metadata == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(metadata);
    }
}
