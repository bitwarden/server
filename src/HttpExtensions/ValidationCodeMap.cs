using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Bit.HttpExtensions;

/// <summary>
/// Works out which code and parameters a validation failure should be reported under.
/// </summary>
/// <remarks>
/// <para>
/// The framework decides <em>whether</em> a value is valid and records a message; the constraint that produced it
/// is discarded on the way. This recovers the constraint, so the <c>200</c> in <c>[StringLength(200)]</c> reaches
/// the client instead of being buried in prose.
/// </para>
/// <para>
/// There are two ways in. <see cref="TryResolveRegistered"/> reads a map a generator supplied at build time and
/// touches no reflection, which is what a trimmed or ahead-of-time published app needs.
/// <see cref="TryResolve"/> falls back to walking the model when nothing was registered for it, and says so with
/// <see cref="RequiresUnreferencedCodeAttribute"/>. Controllers take the second path — MVC declares itself
/// unsupported under trimming, so there is nothing there for a generator to save.
/// </para>
/// </remarks>
public static class ValidationCodeMap
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, ValidationCodeEntry>> _registered = new();
    private static readonly ConcurrentDictionary<(Type Root, string Path), ValidationCodeEntry?> _reflected = new();

    /// <summary>
    /// Adds the paths reachable from <paramref name="rootType"/>. Called from generated module initializers, so
    /// it runs before any request is served and never while one is being resolved.
    /// </summary>
    /// <remarks>
    /// Keyed by the type the paths are rooted at, not by the path alone. Two request models both carrying a
    /// <c>Name</c> would otherwise share one entry and the second would silently take the first's code.
    /// </remarks>
    public static void Register(Type rootType, IEnumerable<KeyValuePair<string, ValidationCodeEntry>> entries)
    {
        ArgumentNullException.ThrowIfNull(rootType);
        ArgumentNullException.ThrowIfNull(entries);

        _registered[rootType] = entries.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// Resolves against the generated map only, never reflecting.
    /// </summary>
    /// <remarks>
    /// The entry point for anything that has to survive trimming. Returns false for a model no generator covered,
    /// rather than quietly reaching for reflection that would not be there after publish.
    /// </remarks>
    public static bool TryResolveRegistered(
        Type rootType, string validationPath, string message, out string wirePath, out ErrorCode error)
    {
        ArgumentNullException.ThrowIfNull(rootType);
        ArgumentNullException.ThrowIfNull(validationPath);

        var (normalized, indices) = Normalize(validationPath);
        var entry = _registered.TryGetValue(rootType, out var entries) && entries.TryGetValue(normalized, out var found)
            ? found
            : null;

        return Complete(entry, message, indices, out wirePath, out error);
    }

    /// <summary>
    /// Resolves against the generated map, falling back to walking <paramref name="rootType"/> when nothing was
    /// registered for it.
    /// </summary>
    /// <returns>False when the path names nothing constrained, leaving the caller to report it uncoded.</returns>
    [RequiresUnreferencedCode(
        "Falls back to walking the request model's properties when no generated map covers it. Reachable from MVC "
        + "model binding, which does not support trimming or native AOT either.")]
    public static bool TryResolve(
        Type rootType, string validationPath, string message, out string wirePath, out ErrorCode error)
    {
        ArgumentNullException.ThrowIfNull(rootType);
        ArgumentNullException.ThrowIfNull(validationPath);

        if (_registered.ContainsKey(rootType))
        {
            return TryResolveRegistered(rootType, validationPath, message, out wirePath, out error);
        }

        var (normalized, indices) = Normalize(validationPath);
        var entry = _reflected.GetOrAdd((rootType, normalized), static key => Describe(key.Root, key.Path));

        return Complete(entry, message, indices, out wirePath, out error);
    }

    /// <summary>Forgets what reflection worked out. For tests that redefine a model between cases.</summary>
    internal static void Clear()
    {
        _registered.Clear();
        _reflected.Clear();
    }

    private static bool Complete(
        ValidationCodeEntry? entry, string message, List<int> indices, out string wirePath, out ErrorCode error)
    {
        if (entry is null || Select(entry, message) is not { } candidate)
        {
            wirePath = string.Empty;
            error = null!;
            return false;
        }

        wirePath = Reindex(entry.WirePath, indices);
        error = new ErrorCode(candidate.Code, message, Parameters(candidate));
        return true;
    }

    /// <summary>
    /// Picks the candidate whose constraint would have produced this message, or the one left as the fallback.
    /// </summary>
    /// <remarks>
    /// A property carrying one constraint cannot be ambiguous, so its single candidate is the fallback and answers
    /// whatever the framework said — which keeps the common case independent of framework wording entirely.
    /// </remarks>
    private static ValidationCodeCandidate? Select(ValidationCodeEntry entry, string message)
    {
        ValidationCodeCandidate? fallback = null;

        foreach (var candidate in entry.Candidates)
        {
            if (candidate.Message is null)
            {
                // At most one candidate is left unformattable; a second would make the choice a guess.
                if (fallback is not null)
                {
                    return null;
                }

                fallback = candidate;
                continue;
            }

            if (string.Equals(Safe(candidate.Message, entry.DisplayName), message, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return fallback;
    }

    /// <summary>
    /// Asks the constraint how it words itself, or null if it will not say.
    /// </summary>
    /// <remarks>
    /// A custom attribute is free to throw from <see cref="ValidationAttribute.FormatErrorMessage"/> when handed a
    /// name out of context. Losing the tie-break costs the failure its code; letting the exception out would cost
    /// the caller its response.
    /// </remarks>
    private static string? Safe(Func<string, string> message, string displayName)
    {
        try
        {
            return message(displayName);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static JsonObject? Parameters(ValidationCodeCandidate candidate)
    {
        if (candidate.Parameters is not { Count: > 0 } source)
        {
            return null;
        }

        var parameters = new JsonObject();
        foreach (var (name, value) in source)
        {
            parameters[name] = value switch
            {
                null => null,
                string text => JsonValue.Create(text),
                int number => JsonValue.Create(number),
                long number => JsonValue.Create(number),
                double number => JsonValue.Create(number),
                decimal number => JsonValue.Create(number),
                bool flag => JsonValue.Create(flag),
                _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture)),
            };
        }

        return parameters;
    }

    /// <summary>
    /// Follows <paramref name="normalizedPath"/> from <paramref name="rootType"/> to the property it names, and
    /// describes what that property constrains. Null when the path leads nowhere, or nowhere constrained.
    /// </summary>
    [RequiresUnreferencedCode("Walks the request model's property graph.")]
    private static ValidationCodeEntry? Describe(Type rootType, string normalizedPath)
    {
        var current = rootType;
        var wirePath = new StringBuilder();
        PropertyInfo? property = null;

        foreach (var rawSegment in normalizedPath.Split('.'))
        {
            var isCollection = rawSegment.EndsWith("[]", StringComparison.Ordinal);
            var name = isCollection ? rawSegment[..^2] : rawSegment;

            property = current?.GetProperty(
                name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (property is null)
            {
                return null;
            }

            if (wirePath.Length > 0)
            {
                wirePath.Append('.');
            }

            wirePath.Append(WireName(property));
            if (isCollection)
            {
                wirePath.Append("[]");
            }

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            current = isCollection ? ElementType(propertyType) : propertyType;
        }

        if (property is null)
        {
            return null;
        }

        var attributes = property.GetCustomAttributes<ValidationAttribute>(inherit: true).ToArray();
        if (attributes.Length == 0)
        {
            return null;
        }

        var displayName = property.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? property.Name;
        var ambiguous = attributes.Length > 1;

        var candidates = new List<ValidationCodeCandidate>(attributes.Length);
        foreach (var attribute in attributes)
        {
            if (Translate(attribute) is not { } translated)
            {
                continue;
            }

            // Only worth asking when there is something to tell apart; the constraint builds the string.
            candidates.Add(ambiguous
                ? translated with { Message = attribute.FormatErrorMessage }
                : translated);
        }

        return candidates.Count == 0
            ? null
            : new ValidationCodeEntry(wirePath.ToString(), displayName, candidates);
    }

    /// <summary>The code and constraint values for one attribute, or null if we have no name for it.</summary>
    private static ValidationCodeCandidate? Translate(ValidationAttribute attribute) => attribute switch
    {
        RequiredAttribute => new(ValidationCodes.Required),

        // One message covers both directions of a two-ended constraint, leaving nothing to tell them apart. The
        // client gets both bounds and composes the sentence itself.
        StringLengthAttribute { MinimumLength: > 0 } length => new(ValidationCodes.InvalidLength, null,
            [new(ValidationParameters.Min, length.MinimumLength), new(ValidationParameters.Max, length.MaximumLength)]),
        StringLengthAttribute length => new(ValidationCodes.TooLong, null,
            [new(ValidationParameters.Max, length.MaximumLength)]),

        MaxLengthAttribute max => new(ValidationCodes.TooLong, null, [new(ValidationParameters.Max, max.Length)]),
        MinLengthAttribute min => new(ValidationCodes.TooShort, null, [new(ValidationParameters.Min, min.Length)]),

        RangeAttribute range => new(ValidationCodes.OutOfRange, null,
            [new(ValidationParameters.Min, range.Minimum), new(ValidationParameters.Max, range.Maximum)]),

        EmailAddressAttribute => new(ValidationCodes.InvalidEmail),
        CompareAttribute compare => new(ValidationCodes.MustMatch, null,
            [new(ValidationParameters.Other, compare.OtherProperty)]),
        RegularExpressionAttribute expression => new(ValidationCodes.InvalidFormat, null,
            [new(ValidationParameters.Pattern, expression.Pattern)]),

        UrlAttribute or PhoneAttribute or CreditCardAttribute => new(ValidationCodes.InvalidFormat),

        _ => null,
    };

    /// <summary>The name the client sent: the JSON name when renamed, otherwise the camel-cased property.</summary>
    [RequiresUnreferencedCode("Reads the property's JSON naming attribute.")]
    private static string WireName(PropertyInfo property)
    {
        if (property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name is { } renamed)
        {
            return renamed;
        }

        return CamelCase(property.Name);
    }

    internal static string CamelCase(string name) =>
        name.Length > 0 && char.IsUpper(name[0]) ? char.ToLowerInvariant(name[0]) + name[1..] : name;

    [RequiresUnreferencedCode("Inspects the collection's interfaces to find its element type.")]
    private static Type? ElementType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        foreach (var candidate in type.GetInterfaces().Append(type))
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return candidate.GetGenericArguments()[0];
            }
        }

        return typeof(IEnumerable).IsAssignableFrom(type) ? typeof(object) : null;
    }

    /// <summary>
    /// Flattens <c>Members[0].Email</c> to <c>Members[].Email</c>, keeping the indices so the reported path can
    /// name the element that actually failed.
    /// </summary>
    private static (string Normalized, List<int> Indices) Normalize(string path)
    {
        var indices = new List<int>();
        if (!path.Contains('[', StringComparison.Ordinal))
        {
            return (path, indices);
        }

        var normalized = new StringBuilder(path.Length);
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] != '[')
            {
                normalized.Append(path[i]);
                continue;
            }

            var close = path.IndexOf(']', i);
            if (close < 0)
            {
                normalized.Append(path, i, path.Length - i);
                break;
            }

            if (int.TryParse(path.AsSpan(i + 1, close - i - 1), out var index))
            {
                indices.Add(index);
                normalized.Append("[]");
            }
            else
            {
                normalized.Append(path, i, close - i + 1);
            }

            i = close;
        }

        return (normalized.ToString(), indices);
    }

    /// <summary>Puts the indices taken out by <see cref="Normalize"/> back into the wire path, in order.</summary>
    private static string Reindex(string wirePath, List<int> indices)
    {
        if (indices.Count == 0)
        {
            return wirePath;
        }

        var result = new StringBuilder(wirePath.Length + (indices.Count * 2));
        var next = 0;
        for (var i = 0; i < wirePath.Length; i++)
        {
            if (wirePath[i] == '[' && i + 1 < wirePath.Length && wirePath[i + 1] == ']' && next < indices.Count)
            {
                result.Append('[').Append(indices[next++]).Append(']');
                i++;
                continue;
            }

            result.Append(wirePath[i]);
        }

        return result.ToString();
    }
}
