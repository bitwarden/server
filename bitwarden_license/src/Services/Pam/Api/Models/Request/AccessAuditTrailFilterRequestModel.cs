using System.ComponentModel.DataAnnotations;
using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// How a read of the organization's access-audit trail is narrowed, as query parameters. An unset dimension matches
/// everything; values within a dimension are OR-ed, dimensions are AND-ed.
/// </summary>
public class AccessAuditTrailFilterRequestModel : IValidatableObject
{
    /// <summary>
    /// Inclusive lower bound on the event's instant. Absent reaches back as far as the retention window allows.
    /// </summary>
    public DateTime? Start { get; set; }

    /// <summary>Inclusive upper bound on the event's instant. Absent reaches up to now.</summary>
    public DateTime? End { get; set; }

    /// <summary>
    /// The event kinds to keep, by their <see cref="AccessAuditEventKindNames"/> name. Repeat the parameter to select
    /// more than one.
    /// </summary>
    public string[]? Kind { get; set; }

    /// <summary>Who performed the event. Repeat the parameter to select more than one.</summary>
    public Guid[]? ActorId { get; set; }

    /// <summary>
    /// Whether to also keep system events, which have no actor. Unions with <see cref="ActorId"/>.
    /// </summary>
    public bool? IncludeAutomatedActor { get; set; }

    /// <summary>The access requester the event concerns. Repeat the parameter to select more than one.</summary>
    public Guid[]? RequesterId { get; set; }

    /// <summary>The subject credentials to keep. Repeat the parameter to select more than one.</summary>
    public Guid[]? CipherId { get; set; }

    /// <summary>
    /// The subject access rules to keep. Repeat the parameter to select more than one. Unions with
    /// <see cref="CipherId"/>.
    /// </summary>
    public Guid[]? RuleId { get; set; }

    /// <summary>The previous page's continuation token. Absent starts at the newest event in range.</summary>
    public string? ContinuationToken { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        TryBuild(out _, out var errors);
        return errors;
    }

    /// <summary>The validated read this describes.</summary>
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
                errors.Add(new ValidationResult($"'{name}' is not a known audit event kind.", [nameof(Kind)]));
            }
        }

        AccessAuditEventCursor? before = null;
        if (!string.IsNullOrEmpty(ContinuationToken))
        {
            if (AccessAuditTrailContinuationToken.TryParse(ContinuationToken, out var occurredDate, out var id))
            {
                before = new AccessAuditEventCursor(occurredDate, id);
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
            Before = before,
        };

        return errors.Count == 0;
    }
}
