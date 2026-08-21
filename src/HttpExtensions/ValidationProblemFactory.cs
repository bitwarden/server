using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.HttpExtensions;

/// <summary>
/// Turns the model state the framework filled in into the coded problem document clients read.
/// </summary>
/// <remarks>
/// Every failure in the model state reaches the document, coded when
/// <see cref="ValidationMessageCodes"/> recognises its message and as <see cref="ValidationCodes.Invalid"/> when
/// it does not — a failure is never dropped for want of a code, because a client that gets a 400 with an empty
/// <c>errors</c> map has been told less than nothing.
/// </remarks>
public static class ValidationProblemFactory
{
    /// <summary>
    /// Builds the document for <paramref name="modelState"/>, keyed by the property names the client sent.
    /// </summary>
    public static BitwardenValidationProblemDetails FromModelState(
        ModelStateDictionary modelState,
        string title = "One or more validation errors occurred.",
        string type = "validation_error",
        int statusCode = StatusCodes.Status400BadRequest)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        var errors = new Dictionary<string, List<ErrorCode>>(StringComparer.Ordinal);

        foreach (var (key, entry) in modelState)
        {
            if (entry.ValidationState != ModelValidationState.Invalid || entry.Errors.Count == 0)
            {
                continue;
            }

            var wirePath = ToWireName(key);
            if (!errors.TryGetValue(wirePath, out var codes))
            {
                codes = [];
                errors[wirePath] = codes;
            }

            foreach (var modelError in entry.Errors)
            {
                codes.Add(ValidationMessageCodes.Resolve(Describe(modelError)));
            }
        }

        return new BitwardenValidationProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Errors = errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal),
        };
    }

    /// <summary>
    /// The message to report, falling back when model binding recorded an exception instead of one.
    /// </summary>
    /// <remarks>
    /// The exception's own message is not used: a binding failure carries framework or serializer detail that
    /// describes our internals rather than the caller's mistake, and it is not ours to put on the wire.
    /// </remarks>
    private static string Describe(ModelError modelError) =>
        !string.IsNullOrEmpty(modelError.ErrorMessage)
            ? modelError.ErrorMessage
            : "The value provided is not valid.";

    /// <summary>
    /// The property path as the client wrote it, matching the camel casing the serializer applies to everything
    /// else.
    /// </summary>
    /// <remarks>
    /// Model state keys the CLR name, and without reading the model there is nothing to consult for a property
    /// renamed by <c>[JsonPropertyName]</c>. Such a property is reported under its camel-cased CLR name, which is
    /// what the previous envelope did too.
    /// </remarks>
    private static string ToWireName(string modelStateKey)
    {
        if (string.IsNullOrEmpty(modelStateKey))
        {
            return string.Empty;
        }

        var segments = modelStateKey.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            segments[i] = CamelCase(segments[i]);
        }

        return string.Join('.', segments);
    }

    private static string CamelCase(string segment) =>
        segment.Length > 0 && char.IsUpper(segment[0])
            ? char.ToLowerInvariant(segment[0]) + segment[1..]
            : segment;
}
