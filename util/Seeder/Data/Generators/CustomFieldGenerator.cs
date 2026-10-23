using Bit.Core.Vault.Enums;
using Bit.Seeder.Data.Static;
using Bit.Seeder.Models;
using Bogus;

namespace Bit.Seeder.Data.Generators;

/// <summary>
/// Deterministic custom fields for generated logins.
/// </summary>
internal sealed class CustomFieldGenerator(int seed)
{
    private static readonly ThreadLocal<Faker> _threadFaker = new(() => new Faker());

    /// <summary>
    /// Builds one to three custom fields with distinct names for the login at <paramref name="index"/>.
    /// The last field is hidden; the others are text.
    /// </summary>
    internal List<FieldViewDto> GenerateByIndex(int index)
    {
        var faker = _threadFaker.Value!;
        faker.Random = new Randomizer(seed + index);

        var names = faker.PickRandom(CustomFieldNames.All, faker.Random.Number(1, 3)).ToList();
        return [.. names
            .Select((name, i) => new FieldViewDto
            {
                Name = name,
                Value = faker.Random.ReplaceNumbers("########"),
                Type = i == names.Count - 1 ? (int)FieldType.Hidden : (int)FieldType.Text
            })];
    }
}
