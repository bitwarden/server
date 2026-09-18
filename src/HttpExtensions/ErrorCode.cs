using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Bit.HttpExtensions;

/// <summary>
/// One failure under a property: a stable <paramref name="Type"/> the client switches on, a human-readable
/// <paramref name="Detail"/>, and the substitutions it needs to render that detail in another language.
/// </summary>
/// <param name="Type">
/// The machine-readable code. Names what went wrong and never the property it is keyed under — <c>required</c>,
/// not <c>name_required</c>. Draw it from <see cref="ValidationCodes"/> rather than spelling one locally.
/// </param>
/// <param name="Detail">
/// The English message. A client that localizes renders from <paramref name="Type"/> and its
/// <paramref name="Parameters"/> instead, so this is a fallback rather than the contract.
/// </param>
/// <param name="Parameters">
/// The substitutions a client needs to render its own message for <paramref name="Type"/> — a length limit, a
/// range bound. Omitted from the body when null, so a code needing no substitution costs nothing. Carries the
/// limit that was breached and never anything derived from the value that breached it: a length ceiling, never
/// the string that overran it. Key it from <see cref="ValidationParameters"/>.
/// </param>
/// <remarks>
/// <see cref="JsonObject"/> rather than a dictionary of <c>object</c> so the document stays serializable without
/// reflection. An <c>object</c> value has to have its runtime type resolved when it is written, which is the one
/// thing a source-generated — and so trim- and AOT-safe — serializer cannot do.
/// </remarks>
public sealed record ErrorCode(
    string Type,
    string Detail,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonObject? Parameters = null);
