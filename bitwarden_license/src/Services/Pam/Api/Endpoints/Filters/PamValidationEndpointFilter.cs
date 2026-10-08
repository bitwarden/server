using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Api;

namespace Bit.Services.Pam.Api.Endpoints.Filters;

/// <summary>
/// Minimal API equivalent of the MVC <c>ModelStateValidationFilterAttribute</c>. Also validates nested request models,
/// which <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}, bool)"/>
/// does not recurse into.
/// </summary>
public class PamValidationEndpointFilter : IEndpointFilter
{
    // Matched by prefix and suffix, so request models in nested features such as AccessConnector.Rotation are covered.
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
