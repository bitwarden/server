using Bit.Seeder.Data.Enums;
using Bogus;
using Bogus.DataSets;

namespace Bit.Seeder.Data;

/// <summary>
/// Provides locale-aware name generation using the Bogus library.
/// Maps GeographicRegion to appropriate Bogus locales for culturally-appropriate names.
/// </summary>
internal sealed class BogusNameProvider
{
    private readonly Faker _faker;

    public BogusNameProvider(GeographicRegion region, int? seed = null)
    {
        var locale = MapRegionToLocale(region, seed);
        _faker = seed.HasValue
            ? new Faker(locale) { Random = new Randomizer(seed.Value) }
            : new Faker(locale);
    }

    public string FirstName() => _faker.Name.FirstName();

    public string FirstName(Name.Gender gender) => _faker.Name.FirstName(gender);

    public string LastName() => _faker.Name.LastName();

    private static string MapRegionToLocale(GeographicRegion region, int? seed) => region switch
    {
        GeographicRegion.NorthAmerica => "en_US",
        GeographicRegion.Europe => GetRandomEuropeanLocale(seed),
        GeographicRegion.AsiaPacific => GetRandomAsianLocale(seed),
        GeographicRegion.LatinAmerica => GetRandomLatinAmericanLocale(seed),
        GeographicRegion.MiddleEast => GetRandomMiddleEastLocale(seed),
        GeographicRegion.Africa => GetRandomAfricanLocale(seed),
        GeographicRegion.Global => "en",
        _ => "en"
    };

    private static string GetRandomEuropeanLocale(int? seed)
    {
        var locales = new[] { "en_GB", "de", "fr", "es", "it", "nl", "pl", "pt_PT", "sv" };
        return PickLocale(locales, seed);
    }

    private static string GetRandomAsianLocale(int? seed)
    {
        var locales = new[] { "ja", "ko", "zh_CN", "zh_TW", "vi" };
        return PickLocale(locales, seed);
    }

    private static string GetRandomLatinAmericanLocale(int? seed)
    {
        var locales = new[] { "es_MX", "pt_BR", "es" };
        return PickLocale(locales, seed);
    }

    private static string GetRandomMiddleEastLocale(int? seed)
    {
        // Bogus has limited Middle East support; use available Arabic/Turkish locales
        var locales = new[] { "ar", "tr", "fa" };
        return PickLocale(locales, seed);
    }

    private static string GetRandomAfricanLocale(int? seed)
    {
        // Bogus has limited African support; use South African English and French (West Africa)
        var locales = new[] { "en_ZA", "fr" };
        return PickLocale(locales, seed);
    }

    private static string PickLocale(string[] locales, int? seed)
    {
        var random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        return locales[random.Next(locales.Length)];
    }
}
