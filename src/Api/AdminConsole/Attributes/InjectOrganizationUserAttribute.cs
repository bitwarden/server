using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Bit.Api.AdminConsole.Attributes;

/// <summary>
/// Binds a <see cref="Bit.Core.Entities.OrganizationUser"/> parameter by loading it from the database
/// and validating that it belongs to the organization identified by the <c>orgId</c> or
/// <c>organizationId</c> route parameter.
/// </summary>
/// <remarks>
/// The organization user is resolved from the route parameter named by
/// <see cref="OrganizationUserIdRouteParam"/> (default <c>"id"</c>). If the user is not found or
/// does not belong to the organization, a <see cref="Bit.Core.Exceptions.NotFoundException"/> is thrown.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// [HttpPut("{id}/recover-account")]
/// [Authorize<ManageAccountRecoveryRequirement>]
/// public async Task<IResult> PutRecoverAccount(Guid orgId, Guid id,
///     [FromBody] OrganizationUserResetPasswordRequestModel model,
///     [InjectOrganizationUser] OrganizationUser targetOrganizationUser)
///
/// [HttpPost("{organizationUserId}/accept")]
/// public async Task<IResult> AcceptAsync(Guid organizationUserId,
///     [InjectOrganizationUser("organizationUserId")] OrganizationUser organizationUser)
/// ]]></code>
/// </example>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class InjectOrganizationUserAttribute(string organizationUserIdRouteParam = "id")
    : ModelBinderAttribute(typeof(OrganizationUserModelBinder))
{
    /// <summary>
    /// Name of the route parameter containing the organization user ID. Defaults to <c>"id"</c>.
    /// </summary>
    public string OrganizationUserIdRouteParam { get; } = organizationUserIdRouteParam;
}

/// <summary>
/// Custom model binder that loads an <see cref="Bit.Core.Entities.OrganizationUser"/> from the database,
/// validates that it belongs to the organization identified by the route, and binds it to the parameter.
/// </summary>
/// <remarks>
/// This binder is used via the <see cref="InjectOrganizationUserAttribute"/>.
/// </remarks>
public class OrganizationUserModelBinder : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var defaultMetadata = bindingContext.ModelMetadata as DefaultModelMetadata;
        var attr = defaultMetadata?.Attributes.ParameterAttributes
            ?.OfType<InjectOrganizationUserAttribute>()
            .FirstOrDefault()
            ?? new InjectOrganizationUserAttribute();

        Guid orgId;
        try
        {
            orgId = bindingContext.HttpContext.GetOrganizationId();
        }
        catch (InvalidOperationException)
        {
            throw new BadRequestException("Route parameter 'orgId' or 'organizationId' is missing or invalid.");
        }

        var routeValues = bindingContext.ActionContext.RouteData.Values;
        if (!routeValues.TryGetValue(attr.OrganizationUserIdRouteParam, out var idValue)
            || !Guid.TryParse(idValue?.ToString(), out var orgUserId))
        {
            throw new BadRequestException(
                $"Route parameter '{attr.OrganizationUserIdRouteParam}' is missing or invalid.");
        }

        var repo = bindingContext.HttpContext.RequestServices
            .GetRequiredService<IOrganizationUserRepository>();

        var organizationUser = await repo.GetByIdAsync(orgUserId);
        if (organizationUser is null || organizationUser.OrganizationId != orgId)
        {
            throw new NotFoundException();
        }

        bindingContext.Result = ModelBindingResult.Success(organizationUser);
    }
}
