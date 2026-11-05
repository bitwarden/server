using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.Api.AdminConsole.Endpoints.Filters;

public static class RequestBodyValidationEndpointFilter
{
    public static EndpointFilterDelegate Create(EndpointFilterFactoryContext context, EndpointFilterDelegate next)
    {
        var bodyIndex = Array.FindIndex(
            context.MethodInfo.GetParameters(),
            parameter => parameter.GetCustomAttribute<FromBodyAttribute>() is not null);

        if (bodyIndex < 0)
        {
            return next;
        }

        return invocationContext =>
        {
            var model = invocationContext.Arguments[bodyIndex];
            var results = new List<ValidationResult>();

            if (model is not null && Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true))
            {
                return next(invocationContext);
            }

            var modelState = new ModelStateDictionary();
            if (model is null)
            {
                modelState.AddModelError(string.Empty, "Body is empty.");
            }

            foreach (var result in results)
            {
                modelState.AddModelError(result.MemberNames.FirstOrDefault() ?? string.Empty, result.ErrorMessage ?? string.Empty);
            }

            return ValueTask.FromResult<object?>(TypedResults.BadRequest(new ErrorResponseModel(modelState)));
        };
    }
}
