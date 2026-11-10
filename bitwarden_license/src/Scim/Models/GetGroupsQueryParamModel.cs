using System.ComponentModel.DataAnnotations;

namespace Bit.Scim.Models;

public class GetGroupsQueryParamModel
{
    public string Filter { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Count { get; init; } = 50;

    [Range(1, int.MaxValue)]
    public int StartIndex { get; init; } = 1;

    /// <summary>
    /// Limits members only when explicitly supplied; all members are returned by default.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MemberCount { get; init; } = int.MaxValue;

    [Range(1, int.MaxValue)]
    public int MemberStartIndex { get; init; } = 1;
}
