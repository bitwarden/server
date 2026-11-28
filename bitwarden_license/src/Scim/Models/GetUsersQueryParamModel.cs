using System.ComponentModel.DataAnnotations;

namespace Bit.Scim.Models;

public class GetUsersQueryParamModel
{
    public string Filter { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Count { get; init; } = 50;

    [Range(1, int.MaxValue)]
    public int StartIndex { get; init; } = 1;

    /// <summary>
    /// Limits group memberships only when explicitly supplied; all memberships are returned by default.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int GroupCount { get; init; } = int.MaxValue;

    [Range(1, int.MaxValue)]
    public int GroupStartIndex { get; init; } = 1;
}
