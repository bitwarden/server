using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.HttpExtensions;

/// <summary>
/// The body a <see cref="BitwardenValidationProblemResult"/> writes: the RFC 7807 members plus an
/// <c>errors</c> map keyed by the property that failed, each entry carrying a code the client switches on.
/// </summary>
/// <remarks>
/// Named as a type rather than assembled into <see cref="ProblemDetails.Extensions"/> so that the document a
/// client parses and the document OpenAPI describes are the same declaration, and neither can drift from the
/// other.
/// </remarks>
public sealed class BitwardenValidationProblemDetails : ProblemDetails
{
    /// <summary>The document member the errors map is written under.</summary>
    internal const string ErrorsMember = "errors";

    /// <summary>
    /// The failures, keyed by the property name as it appeared on the wire. A property that failed several ways
    /// carries an entry per failure.
    /// </summary>
    /// <remarks>
    /// Ordered after the inherited members so the code that reads the document meets <c>type</c> and
    /// <c>status</c> before the detail of what went wrong, as it does in every other problem response.
    /// </remarks>
    [JsonPropertyOrder(100)]
    [JsonPropertyName(ErrorsMember)]
    public IDictionary<string, ErrorCode[]> Errors { get; init; } = new Dictionary<string, ErrorCode[]>();
}
