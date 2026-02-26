using Bit.Seeder.Models;

namespace Bit.Seeder.Factories;

/// <summary>
/// Shared mapping helpers for converting SeedItem fields to CipherViewDto fields.
/// </summary>
internal static class SeedItemMapping
{
    internal static int MapFieldType(string type) => type switch
    {
        "hidden" => 1,
        "boolean" => 2,
        "linked" => 3,
        _ => 0 // text
    };

    internal static List<FieldViewDto>? MapFields(List<SeedField>? fields) =>
        fields?.Select(f => new FieldViewDto
        {
            Name = f.Name,
            Value = f.Value,
            Type = MapFieldType(f.Type)
        }).ToList();

    internal static int MapUriMatchType(string match) => match switch
    {
        "host" => 1,
        "startsWith" => 2,
        "exact" => 3,
        "regex" => 4,
        "never" => 5,
        _ => 0 // domain
    };
}
