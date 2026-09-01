using System.ComponentModel.DataAnnotations;
using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// How a read of the organization's access-audit trail is narrowed, as query parameters. Every dimension is optional
/// and an unset one matches everything, so a bare <c>GET</c> still reads the trail — one page of it, newest first.
///
/// The dimensions mirror the Admin Console's filter chips, and each is a list because those chips are multi-select:
/// an auditor reconstructing an incident is usually following two or three people, not one. Values within a dimension
/// are OR-ed, dimensions are AND-ed together.
/// </summary>
public class AccessAuditTrailFilterRequestModel : IValidatableObject
{
    /// <summary>
    /// Inclusive lower bound on the event's instant. Absent reaches back as far as the retention window allows, which
    /// is also as far as the store holds anything.
    /// </summary>
    public DateTime? Start { get; set; }

    /// <summary>Inclusive upper bound on the event's instant. Absent reaches up to now.</summary>
    public DateTime? End { get; set; }

    /// <summary>
    /// The event kinds to keep, in the governance vocabulary the response reports
    /// (<see cref="AccessAuditEventKindNames"/>) — <c>requestApproved</c>, not the underlying number. Repeat the
    /// parameter to select more than one.
    /// </summary>
    public string[]? Kind { get; set; }

    /// <summary>Who performed the event. Repeat the parameter to select more than one.</summary>
    public Guid[]? ActorId { get; set; }

    /// <summary>
    /// Whether to also keep the system / automatic events, which have no actor id to be selected by. Unions with
    /// <see cref="ActorId"/> rather than narrowing it, and on its own selects the automatic events alone.
    /// </summary>
    /// <remarks>
    /// Nullable so the parameter stays optional: <c>[AsParameters]</c> treats a non-nullable value type as required
    /// and answers a request that omits it with a 400, which would make a bare read of the trail impossible.
    /// </remarks>
    public bool? IncludeAutomatedActor { get; set; }

    /// <summary>The access requester the event concerns. Repeat the parameter to select more than one.</summary>
    public Guid[]? RequesterId { get; set; }

    /// <summary>
    /// The subject credentials to keep. Repeat the parameter to select more than one.
    /// </summary>
    public Guid[]? CipherId { get; set; }

    /// <summary>
    /// The subject access rules to keep. Repeat the parameter to select more than one.
    ///
    /// Unions with <see cref="CipherId"/> rather than narrowing it — the two are the halves of one Item selection, and
    /// a rule-administration event names a rule and no cipher, so asking for a credential and a rule together must
    /// mean either rather than the empty intersection.
    /// </summary>
    public Guid[]? RuleId { get; set; }

    /// <summary>
    /// Where the previous page stopped, as that page's response reported it. Absent starts at the newest event in
    /// range.
    /// </summary>
    public string? ContinuationToken { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        TryBuild(out _, out var errors);
        return errors;
    }

    /// <summary>
    /// The validated read this describes.
    /// </summary>
    /// <remarks>
    /// <c>PamValidationEndpointFilter</c> has already run <see cref="Validate"/> by the time a handler calls
    /// this, so the throw is unreachable from the endpoint; it is here so the same guarantee holds for any other
    /// caller rather than being silently assumed.
    /// </remarks>
    public AccessAuditTrailQueryOptions ToQueryOptions()
    {
        if (!TryBuild(out var options, out var errors))
        {
            throw new BadRequestException(
                string.Join(" ", errors.Select(error => error.ErrorMessage)));
        }

        return options;
    }

    private bool TryBuild(out AccessAuditTrailQueryOptions options, out List<ValidationResult> errors)
    {
        errors = [];

        var kinds = new List<AccessAuditEventKind>();
        foreach (var name in Kind ?? [])
        {
            if (AccessAuditEventKindNames.TryParse(name, out var kind))
            {
                kinds.Add(kind);
            }
            else
            {
                // Named rather than ignored: a filter the server did not understand would otherwise be reported as a
                // trail with nothing in it, which on an audit surface reads as "this never happened".
                errors.Add(new ValidationResult($"'{name}' is not a known audit event kind.", [nameof(Kind)]));
            }
        }

        DateTime? beforeOccurredAt = null;
        Guid? beforeId = null;
        if (!string.IsNullOrEmpty(ContinuationToken))
        {
            if (AccessAuditTrailContinuationToken.TryParse(ContinuationToken, out var occurredAt, out var id))
            {
                beforeOccurredAt = occurredAt;
                beforeId = id;
            }
            else
            {
                errors.Add(new ValidationResult(
                    "The continuation token is not one this endpoint issued.", [nameof(ContinuationToken)]));
            }
        }

        options = new AccessAuditTrailQueryOptions
        {
            Start = Start.ToUtc(),
            End = End.ToUtc(),
            Kinds = kinds,
            ActorIds = ActorId ?? [],
            IncludeAutomatedActor = IncludeAutomatedActor ?? false,
            RequesterIds = RequesterId ?? [],
            CipherIds = CipherId ?? [],
            RuleIds = RuleId ?? [],
            BeforeOccurredAt = beforeOccurredAt,
            BeforeId = beforeId,
        };

        return errors.Count == 0;
    }
}
