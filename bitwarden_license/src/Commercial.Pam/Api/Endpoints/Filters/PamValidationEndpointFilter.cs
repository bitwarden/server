using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Api;

namespace Bit.Commercial.Pam.Api.Endpoints.Filters;

/// <summary>
/// Minimal API equivalent of the MVC <c>ModelStateValidationFilterAttribute</c>: runs DataAnnotations validation
/// (including <see cref="IValidatableObject"/>) over the request-model arguments and, on failure, short-circuits
/// with Bitwarden's internal <see cref="ErrorResponseModel"/> 400 — the same body the controllers produced.
/// </summary>
public class PamValidationEndpointFilter : IEndpointFilter
{
    private const string RequestModelNamespace = "Bit.Commercial.Pam.Api.Models.Request";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument is null || argument.GetType().Namespace != RequestModelNamespace)
            {
                continue;
            }

            var results = new List<ValidationResult>();
            if (Validator.TryValidateObject(argument, new ValidationContext(argument), results, validateAllProperties: true))
            {
                continue;
            }

            var validationErrors = results
                .SelectMany(
                    result => result.MemberNames.Any() ? result.MemberNames : [string.Empty],
                    (result, member) => (member, message: result.ErrorMessage ?? string.Empty))
                .GroupBy(error => error.member)
                .ToDictionary(group => group.Key, group => (IEnumerable<string>)group.Select(error => error.message).ToArray());

            return Results.Json(
                new ErrorResponseModel("The model state is invalid.", validationErrors),
                statusCode: StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}
