using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Api;

namespace Bit.Services.Pam.Api.Endpoints.Filters;

/// <summary>
/// Minimal API equivalent of the MVC <c>ModelStateValidationFilterAttribute</c>: runs DataAnnotations validation
/// (including <see cref="IValidatableObject"/>) over the request-model arguments and, on failure, short-circuits
/// with Bitwarden's internal <see cref="ErrorResponseModel"/> 400 — the same body the controllers produced.
/// </summary>
/// <remarks>
/// <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}, bool)"/> does not
/// recurse into complex properties, so nested request models are walked explicitly. MVC's model validator does
/// recurse, and without this a nested model's own attributes and <see cref="IValidatableObject"/> rules would never
/// run — the rotation password policy is only ever reached as a nested property.
/// </remarks>
public class PamValidationEndpointFilter : IEndpointFilter
{
    // A prefix/suffix match rather than an exact one, so nested feature subtrees that mirror the same
    // Api/Models/Request folder convention -- e.g. Rotation's Bit.Services.Pam.AccessConnector.Api.Models.Request --
    // are covered without this filter needing to know about every subtree by name.
    private const string RequestModelNamespacePrefix = "Bit.Services.Pam.";
    private const string RequestModelNamespaceSuffix = ".Api.Models.Request";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (!IsRequestModel(argument))
            {
                continue;
            }

            var results = new List<ValidationResult>();
            Validate(argument!, results, new HashSet<object>(ReferenceEqualityComparer.Instance));
            if (results.Count == 0)
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

    /// <remarks>
    /// The <paramref name="visited"/> set is reference-based, so a model that cycles back to an ancestor terminates
    /// rather than recursing forever.
    /// </remarks>
    private static void Validate(object model, List<ValidationResult> results, HashSet<object> visited)
    {
        if (!visited.Add(model))
        {
            return;
        }

        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        foreach (var property in model.GetType().GetProperties())
        {
            if (property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            var value = property.GetValue(model);
            if (IsRequestModel(value))
            {
                Validate(value!, results, visited);
            }
            else if (value is System.Collections.IEnumerable items and not string)
            {
                foreach (var item in items)
                {
                    if (IsRequestModel(item))
                    {
                        Validate(item!, results, visited);
                    }
                }
            }
        }
    }

    private static bool IsRequestModel(object? value) =>
        value is not null
        && value.GetType().Namespace is { } ns
        && ns.StartsWith(RequestModelNamespacePrefix, StringComparison.Ordinal)
        && ns.EndsWith(RequestModelNamespaceSuffix, StringComparison.Ordinal);
}
