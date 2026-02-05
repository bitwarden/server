using System.Globalization;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Models;
using Bogus;

namespace Bit.Seeder.Data.Generators;

internal sealed class CardDataGenerator
{
    private readonly int _seed;
    private readonly GeographicRegion _region;

    private static readonly Dictionary<GeographicRegion, string[]> _regionalBrands = new()
    {
        [GeographicRegion.NorthAmerica] = ["Visa", "Mastercard", "Amex", "Discover"],
        [GeographicRegion.Europe] = ["Visa", "Mastercard", "Maestro", "Amex"],
        [GeographicRegion.AsiaPacific] = ["Visa", "Mastercard", "JCB", "UnionPay"],
        [GeographicRegion.LatinAmerica] = ["Visa", "Mastercard", "Elo", "Amex"],
        [GeographicRegion.MiddleEast] = ["Visa", "Mastercard", "Amex"],
        [GeographicRegion.Africa] = ["Visa", "Mastercard"],
        [GeographicRegion.Global] = ["Visa", "Mastercard", "Amex", "Discover", "JCB", "UnionPay", "Maestro", "Elo"]
    };

    internal CardDataGenerator(int seed, GeographicRegion region = GeographicRegion.Global)
    {
        _seed = seed;
        _region = region;
    }

    /// <summary>
    /// Generates a deterministic card based on index.
    /// </summary>
    internal CardViewDto GenerateByIndex(int index)
    {
        var seededFaker = new Faker { Random = new Randomizer(_seed + index) };
        var brands = _regionalBrands[_region];
        var brand = brands[index % brands.Length];

        return new CardViewDto
        {
            CardholderName = seededFaker.Name.FullName(),
            Brand = brand,
            Number = GenerateNumber(brand, seededFaker),
            ExpMonth = ((index % 12) + 1).ToString("D2", CultureInfo.InvariantCulture),
            ExpYear = (DateTime.Now.Year + (index % 5) + 1).ToString(CultureInfo.InvariantCulture),
            Code = GenerateCode(brand, seededFaker)
        };
    }

    private static string GenerateNumber(string brand, Faker faker) => brand switch
    {
        // North American / Global
        "Visa" => "4" + faker.Random.ReplaceNumbers("###############"),
        "Mastercard" => faker.PickRandom("51", "52", "53", "54", "55") + faker.Random.ReplaceNumbers("##############"),
        "Amex" => faker.PickRandom("34", "37") + faker.Random.ReplaceNumbers("#############"),
        "Discover" => "6011" + faker.Random.ReplaceNumbers("############"),

        // Europe
        "Maestro" => faker.PickRandom("5018", "5020", "5038", "5893", "6304") + faker.Random.ReplaceNumbers("############"),

        // Asia Pacific
        "JCB" => "35" + faker.Random.ReplaceNumbers("##############"),
        "UnionPay" => "62" + faker.Random.ReplaceNumbers("##############"),

        // Latin America
        "Elo" => faker.PickRandom("4011", "4312", "4389", "5041", "5066", "5067", "6277", "6362", "6363") + faker.Random.ReplaceNumbers("############"),

        _ => faker.Finance.CreditCardNumber()
    };

    private static string GenerateCode(string brand, Faker faker) =>
        brand == "Amex"
            ? faker.Random.Int(1000, 9999).ToString(CultureInfo.InvariantCulture)
            : faker.Random.Int(100, 999).ToString(CultureInfo.InvariantCulture);
}
