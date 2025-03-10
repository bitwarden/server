using System.ComponentModel.DataAnnotations;

public class GetUsersQueryParamModel
{
    public string Filter { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Count { get; init; } = 50;

    [Range(1, int.MaxValue)]
    public int StartIndex { get; init; } = 1;
}
