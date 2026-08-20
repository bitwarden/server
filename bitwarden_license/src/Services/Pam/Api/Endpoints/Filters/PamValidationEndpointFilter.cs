using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Api;

namespace Bit.Services.Pam.Api.Endpoints.Filters;

/// <summary>
/// Minimal API equivalent of the MVC <c>ModelStateValidationFilterAttribute</c>: runs DataAnnotations validation
/// (including <see cref="IValidatableObject"/>) over the request-model arguments and, on failure, short-circuits
/// with Bitwarden's internal <see cref="ErrorResponseModel"/> 400 — the same body the controllers produced.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately uncoded, unlike the failures a handler returns through <c>PamErrorResult</c>. A stable code earns
/// its place when a client acts on that failure differently — reconcile, mark a control, offer another form — and a
/// request that never bound cleanly is not one of those: there is nothing to do but report a malformed request.
/// Coding these would also give a client two vocabularies for one user-facing condition, since the attributes here
/// overlap the domain errors in <c>Bit.Services.Pam.Errors</c> that name the same failures more precisely.
/// </para>
/// <para>
/// The built-in .NET 10 <c>AddValidation()</c> could replace the walk below, but not the response: it keys entries
/// by the CLR property name rather than the serialized one, and answers with <c>HttpValidationProblemDetails</c>
/// rather than this body. Note also that its errors are <c>IDictionary&lt;string, string[]&gt;</c>, so it could not
/// carry codes here even if we later decided we wanted them.
/// </para>
/// </remarks>
public class PamValidationEndpointFilter : IEndpointFilter
{
    private const string RequestModelNamespace = "Bit.Services.Pam.Api.Models.Request";

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
