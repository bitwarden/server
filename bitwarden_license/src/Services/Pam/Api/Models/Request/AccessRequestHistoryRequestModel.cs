using System.ComponentModel.DataAnnotations;
using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// Which page of the approver history to read, and what to narrow it to, as query parameters. Values within a filter
/// are OR-ed, filters are AND-ed.
/// </summary>
public class AccessRequestHistoryRequestModel : IValidatableObject
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    /// <summary>Highest page number, so the row offset stays within an <c>int</c>.</summary>
    private const int MaxPage = int.MaxValue / MaxPageSize - 1;

    private static readonly IReadOnlyDictionary<string, AccessRequestHistoryStatus> _statusNames =
        new Dictionary<string, AccessRequestHistoryStatus>
        {
            ["denied"] = AccessRequestHistoryStatus.Denied,
            ["revoked"] = AccessRequestHistoryStatus.Revoked,
            ["canceled"] = AccessRequestHistoryStatus.Canceled,
            ["expired"] = AccessRequestHistoryStatus.Expired,
            ["sessionEnded"] = AccessRequestHistoryStatus.SessionEnded,
        };

    /// <summary>0-based. Absent reads the first page.</summary>
    public int? Page { get; set; }

    /// <summary>Rows per page, 1 to 100. Absent reads 25.</summary>
    public int? PageSize { get; set; }

    /// <summary>
    /// How the request ended: <c>denied</c>, <c>revoked</c>, <c>canceled</c>, <c>expired</c> or <c>sessionEnded</c>.
    /// Repeat the parameter to select more than one.
    /// </summary>
    public string[]? Status { get; set; }

    /// <summary>The request's collection. Repeat the parameter to select more than one.</summary>
    public Guid[]? CollectionId { get; set; }

    /// <summary>Who made the request. Repeat the parameter to select more than one.</summary>
    public Guid[]? RequesterId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        TryBuild(out _, out var errors);
        return errors;
    }

    /// <summary>The validated read this describes.</summary>
    public AccessRequestHistoryQueryOptions ToQueryOptions()
    {
        if (!TryBuild(out var options, out var errors))
        {
            throw new BadRequestException(string.Join(" ", errors.Select(error => error.ErrorMessage)));
        }

        return options;
    }

    private bool TryBuild(out AccessRequestHistoryQueryOptions options, out List<ValidationResult> errors)
    {
        errors = [];

        var page = Page ?? 0;
        if (page is < 0 or > MaxPage)
        {
            errors.Add(new ValidationResult($"The page must be between 0 and {MaxPage}.", [nameof(Page)]));
        }

        var pageSize = PageSize ?? DefaultPageSize;
        if (pageSize is < 1 or > MaxPageSize)
        {
            errors.Add(new ValidationResult($"The page size must be between 1 and {MaxPageSize}.", [nameof(PageSize)]));
        }

        var statuses = new List<AccessRequestHistoryStatus>();
        foreach (var name in Status ?? [])
        {
            if (_statusNames.TryGetValue(name, out var status))
            {
                statuses.Add(status);
            }
            else
            {
                errors.Add(new ValidationResult($"'{name}' is not a known history status.", [nameof(Status)]));
            }
        }

        options = new AccessRequestHistoryQueryOptions
        {
            Page = page,
            PageSize = pageSize,
            Statuses = statuses,
            CollectionIds = CollectionId ?? [],
            RequesterIds = RequesterId ?? [],
        };

        return errors.Count == 0;
    }
}
