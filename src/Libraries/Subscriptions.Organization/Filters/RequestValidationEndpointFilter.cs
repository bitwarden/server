using System.ComponentModel.DataAnnotations;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.Subscriptions.Organization.Filters;

/// <summary>
/// Runs the DataAnnotations rules on the endpoint's <typeparamref name="TRequest"/> argument and rejects a failure
/// as a <see cref="BadRequestException"/> carrying the model state, so the group's basic exception handling answers
/// with the same <c>ErrorResponseModel</c> body MVC controllers return for invalid model state.
/// </summary>
internal sealed class RequestValidationEndpointFilter<TRequest> : IEndpointFilter
    where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().Single();

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true))
        {
            var modelState = new ModelStateDictionary();
            foreach (var result in results)
            {
                foreach (var memberName in result.MemberNames.DefaultIfEmpty(string.Empty))
                {
                    modelState.AddModelError(memberName, result.ErrorMessage ?? string.Empty);
                }
            }

            throw new BadRequestException(modelState);
        }

        return await next(context);
    }
}
