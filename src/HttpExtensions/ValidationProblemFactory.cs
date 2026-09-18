using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.HttpExtensions;

/// <summary>
/// Turns the model state the framework filled in into the coded problem document clients read.
/// </summary>
/// <remarks>
/// The framework detects; this only names what it found. Every failure in the model state reaches the document,
/// coded when <see cref="ValidationCodeMap"/> knows the path and as <see cref="ValidationCodes.Invalid"/> when it
/// does not — a failure is never dropped for want of a code, because a client that gets a 400 with an empty
/// <c>errors</c> map has been told less than nothing.
/// </remarks>
public static class ValidationProblemFactory
{
    /// <summary>
    /// Builds the document for <paramref name="modelState"/>, keyed by the property names the client sent.
    /// </summary>
    /// <param name="rootTypes">
    /// The types model state was bound from — the action's parameters. Paths are looked up under each in turn,
    /// because a model state key carries no indication of which parameter it came from.
    /// </param>
    [RequiresUnreferencedCode(
        "Recovers validation codes by walking the request model's properties. Takes a ModelStateDictionary, so it "
        + "is only reachable from MVC, which does not support trimming or native AOT.")]
    public static BitwardenValidationProblemDetails FromModelState(
        ModelStateDictionary modelState,
        IReadOnlyList<Type> rootTypes,
        string title = "One or more validation errors occurred.",
        string type = "validation_error",
        int statusCode = StatusCodes.Status400BadRequest)
    {
        ArgumentNullException.ThrowIfNull(modelState);
        ArgumentNullException.ThrowIfNull(rootTypes);

        var errors = new Dictionary<string, List<ErrorCode>>(StringComparer.Ordinal);

        foreach (var (key, entry) in modelState)
        {
            if (entry.ValidationState != ModelValidationState.Invalid || entry.Errors.Count == 0)
            {
                continue;
            }

            foreach (var modelError in entry.Errors)
            {
                var message = Describe(modelError);

                if (!TryResolveAny(rootTypes, key, message, out var wirePath, out var error))
                {
                    wirePath = ToWireName(key);
                    error = new ErrorCode(ValidationCodes.Invalid, message);
                }

                if (!errors.TryGetValue(wirePath, out var codes))
                {
                    codes = [];
                    errors[wirePath] = codes;
                }

                codes.Add(error);
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
    /// Looks the path up under each candidate root, taking the first that claims it.
    /// </summary>
    /// <remarks>
    /// A miss is ordinary rather than exceptional: an action taking a route id alongside a body has one root
    /// that knows the path and one that does not.
    /// </remarks>
    [RequiresUnreferencedCode("Resolves through ValidationCodeMap, which walks the request model.")]
    private static bool TryResolveAny(
        IReadOnlyList<Type> rootTypes, string key, string message, out string wirePath, out ErrorCode error)
    {
        for (var i = 0; i < rootTypes.Count; i++)
        {
            if (ValidationCodeMap.TryResolve(rootTypes[i], key, message, out wirePath, out error))
            {
                return true;
            }
        }

        wirePath = string.Empty;
        error = null!;
        return false;
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
    /// Best-effort wire name for a path the map does not know, matching the camel casing the serializer applies
    /// to everything else. Only reached for an unmapped path, where the alternative is reporting the CLR name.
    /// </summary>
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

    private static string CamelCase(string segment)
    {
        if (segment.Length == 0 || !char.IsUpper(segment[0]))
        {
            return segment;
        }

        return char.ToLowerInvariant(segment[0]) + segment[1..];
    }
}
