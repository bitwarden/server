using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.Validation;
using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
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
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        var validator = bindingContext.HttpContext.RequestServices.GetService<IOrganizationAndOrganizationUserValidator>();

        if (validator is null)
        {
            throw new InvalidOperationException("Organization and user validator service is not registered.");
        }

        (await validator.ValidateAsync(new OrganizationScope(orgId.Value), orgUserId.Value))
            .SwitchResult(
                error =>
                {
                    bindingContext.ModelState.AddModelError("request", error.Message);
                    bindingContext.Result = ModelBindingResult.Failed();
                },
                valid => bindingContext.Result = ModelBindingResult.Success(valid));
    }
}
