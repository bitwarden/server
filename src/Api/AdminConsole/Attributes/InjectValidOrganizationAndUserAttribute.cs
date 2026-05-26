using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Bit.Api.AdminConsole.Attributes;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class InjectValidOrganizationAndUserAttribute(
    string organizationIdRouteParameter = "orgId",
    string organizationUserIdRouteParameter = "id") : Attribute
{
    public string OrganizationIdRouteParameter { get; } = organizationIdRouteParameter;
    public string OrganizationUserIdRouteParameter { get; } = organizationUserIdRouteParameter;
}

public class ValidOrganizationAndUserModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context.Metadata is not DefaultModelMetadata metadata)
        {
            return null;
        }

        var attr = metadata.Attributes.ParameterAttributes
            ?.OfType<InjectValidOrganizationAndUserAttribute>()
            .FirstOrDefault();

        return attr is null
            ? null
            : new ValidOrganizationAndUserModelBinder(
                attr.OrganizationIdRouteParameter,
                attr.OrganizationUserIdRouteParameter);
    }
}

public class ValidOrganizationAndUserModelBinder(
    string organizationIdRouteParameter,
    string organizationUserIdRouteParameter) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var orgId = bindingContext.HttpContext.GetOrganizationId(organizationIdRouteParameter);
        var orgUserId = bindingContext.HttpContext.GetOrganizationUserId(organizationUserIdRouteParameter);

        if (orgId is null || orgUserId is null)
        {
            throw new NotFoundException();
        }

        var organizationRepo = bindingContext.HttpContext.RequestServices.GetRequiredService<IOrganizationRepository>();
        var organizationUserRepo = bindingContext.HttpContext.RequestServices.GetRequiredService<IOrganizationUserRepository>();

        var organization = await organizationRepo.GetByIdAsync(orgId.Value);
        if (organization is null)
        {
            throw new NotFoundException();
        }

        var organizationUser = await organizationUserRepo.GetByIdAsync(orgUserId.Value);
        if (organizationUser is null || organization.Id != organizationUser.OrganizationId)
        {
            throw new NotFoundException();
        }

        bindingContext.Result = ModelBindingResult.Success(new ValidOrganizationAndUser
        {
            Organization = organization,
            OrganizationUser = organizationUser
        });
    }
}
