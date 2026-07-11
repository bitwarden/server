using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Api;

namespace Bit.Services.Pam.Api.Endpoints.Filters;

/// <summary>
/// Minimal API equivalent of the MVC <c>ModelStateValidationFilterAttribute</c>: runs DataAnnotations validation
/// (including <see cref="IValidatableObject"/>) over the request-model arguments and, on failure, short-circuits
/// with Bitwarden's internal <see cref="ErrorResponseModel"/> 400 — the same body the controllers produced.
/// </summary>
public class PamValidationEndpointFilter : IEndpointFilter
{
    // A prefix/suffix match rather than an exact one, so nested feature subtrees that mirror the same
    // Api/Models/Request folder convention -- e.g. Rotation's Bit.Services.Pam.Rotation.Api.Models.Request --
    // are covered without this filter needing to know about every subtree by name.
    private const string RequestModelNamespacePrefix = "Bit.Services.Pam.";
    private const string RequestModelNamespaceSuffix = ".Api.Models.Request";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument is null || argument.GetType().Namespace is not { } ns
                || !ns.StartsWith(RequestModelNamespacePrefix, StringComparison.Ordinal)
                || !ns.EndsWith(RequestModelNamespaceSuffix, StringComparison.Ordinal))
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
