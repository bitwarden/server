using Bit.Core.AdminConsole.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.Api.AdminConsole.Attributes;

/// <summary>
/// Binds an <see cref="Organization"/> parameter by loading it from the database
/// using the <c>orgId</c> or <c>organizationId</c> route parameter.
/// </summary>
/// <remarks>
/// If the organization is not found, a <see cref="NotFoundException"/> is thrown.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// [HttpPost("bulk-auto-confirm")]
/// public async Task<IResult> BulkAutomaticallyConfirmOrganizationUsersAsync(
///     [InjectOrganization] Organization organization,
///     [FromBody] OrganizationUserBulkConfirmRequestModel model)
/// ]]></code>
/// </example>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class InjectOrganizationAttribute : ModelBinderAttribute(typeof(OrganizationModelBinder));

/// <summary>
/// Custom model binder that loads an <see cref="Organization"/> from the database
/// using the <c>orgId</c> or <c>organizationId</c> route parameter and binds it to the parameter.
/// </summary>
/// <remarks>
/// This binder is used via the <see cref="InjectOrganizationAttribute"/>.
/// </remarks>
public class OrganizationModelBinder : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        Guid orgId;
        try
        {
            orgId = bindingContext.HttpContext.GetOrganizationId();
        }
        catch (InvalidOperationException)
        {
            throw new BadRequestException("Route parameter 'orgId' or 'organizationId' is missing or invalid.");
        }

        var repo = bindingContext.HttpContext.RequestServices
            .GetRequiredService<IOrganizationRepository>();

        var organization = await repo.GetByIdAsync(orgId);
        if (organization is null)
        {
            throw new NotFoundException();
        }

        bindingContext.Result = ModelBindingResult.Success(organization);
    }
}
