using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Data.Enums;
using Bogus;

namespace Bit.Seeder.Data.Generators;

/// <summary>
/// Generates diverse usernames based on configurable category distributions.
/// Supports corporate emails, personal emails, social handles, employee IDs, and more.
/// Includes locale-aware name generation for culturally-appropriate usernames.
/// </summary>
internal sealed class CipherUsernameGenerator
{
    private const int NamePoolSize = 1500;

    private static readonly string[] PersonalEmailDomains =
        ["fake-gmail.com", "fake-yahoo.com", "fake-outlook.com", "fake-hotmail.com", "fake-icloud.com"];

    private static readonly string[] SocialPlatformPrefixes =
        ["@", "@", "@", ""];  // 75% chance of @ prefix

    private static readonly string[] EuropeanLocales =
        ["en_GB", "de", "fr", "es", "it", "nl", "pl", "pt_PT", "sv"];

    private static readonly string[] AsianLocales =
        ["ja", "ko", "zh_CN", "zh_TW", "vi"];

    private static readonly string[] LatinAmericanLocales =
        ["es_MX", "pt_BR", "es"];

    private static readonly string[] MiddleEastLocales =
        ["ar", "tr", "fa"];

    private static readonly string[] AfricanLocales =
        ["en_ZA", "fr"];

    private readonly int _seed;

    private readonly Distribution<UsernameCategory> _distribution;

    private readonly UsernamePatternType _corporateEmailPattern;

    private readonly string[] _firstNames;

    private readonly string[] _lastNames;

    /// <summary>
    /// Creates a username generator with the specified distribution and settings.
    /// </summary>
    /// <param name="seed">Seed for deterministic generation.</param>
    /// <param name="distribution">Distribution of username categories. Use UsernameDistributions.Realistic for defaults.</param>
    /// <param name="region">Geographic region for culturally-appropriate name generation.</param>
    /// <param name="corporateEmailPattern">Pattern for corporate emails (default: first.last@domain).</param>
    internal CipherUsernameGenerator(
        int seed,
        Distribution<UsernameCategory> distribution,
        GeographicRegion region = GeographicRegion.Global,
        UsernamePatternType corporateEmailPattern = UsernamePatternType.FirstDotLast)
    {
        _seed = seed;
        _distribution = distribution;
        _corporateEmailPattern = corporateEmailPattern;

        // Build locale-aware name pools
        var locale = MapRegionToLocale(region, seed);
        var faker = new Faker(locale) { Random = new Randomizer(seed) };
        _firstNames = Enumerable.Range(0, NamePoolSize).Select(_ => faker.Name.FirstName()).ToArray();
        _lastNames = Enumerable.Range(0, NamePoolSize).Select(_ => faker.Name.LastName()).ToArray();
    }

    /// <summary>
    /// Generates a deterministic username based on index and optional domain.
    /// Category is selected based on the configured distribution.
    /// </summary>
    /// <param name="index">Index for deterministic selection.</param>
    /// <param name="totalHint">Total number of items (for distribution calculation). Default: 1000.</param>
    /// <param name="domain">Corporate domain (used for CorporateEmail category).</param>
    internal string GenerateByIndex(int index, int totalHint = 1000, string? domain = null)
    {
        var category = _distribution.Select(index, totalHint);
        var seededFaker = new Faker { Random = new Randomizer(_seed + index) };

        var offset = GetDeterministicOffset(index);
        var firstName = _firstNames[(index + offset) % _firstNames.Length];
        var lastName = _lastNames[(index * 7 + offset) % _lastNames.Length];

        return category switch
        {
            UsernameCategory.CorporateEmail => GenerateCorporateEmail(firstName, lastName, domain ?? "example.com"),
            UsernameCategory.PersonalEmail => GeneratePersonalEmail(seededFaker, firstName, lastName, index),
            UsernameCategory.SocialHandle => GenerateSocialHandle(seededFaker, firstName, lastName, index),
            UsernameCategory.UsernameOnly => GenerateUsernameOnly(seededFaker, firstName, lastName, index),
            UsernameCategory.EmployeeId => GenerateEmployeeId(seededFaker, index),
            UsernameCategory.PhoneNumber => GeneratePhoneNumber(seededFaker, index),
            UsernameCategory.LegacySystem => GenerateLegacySystem(firstName, lastName, index),
            UsernameCategory.RandomAlphanumeric => GenerateRandomAlphanumeric(seededFaker),
            _ => GenerateCorporateEmail(firstName, lastName, domain ?? "example.com")
        };
    }

    private string GenerateCorporateEmail(string firstName, string lastName, string domain)
    {
        var first = firstName.ToLowerInvariant();
        var last = lastName.ToLowerInvariant();
        var f = char.ToLowerInvariant(firstName[0]);

        return _corporateEmailPattern switch
        {
            UsernamePatternType.FirstDotLast => $"{first}.{last}@{domain}",
            UsernamePatternType.FDotLast => $"{f}.{last}@{domain}",
            UsernamePatternType.FLast => $"{f}{last}@{domain}",
            UsernamePatternType.LastDotFirst => $"{last}.{first}@{domain}",
            UsernamePatternType.First_Last => $"{first}_{last}@{domain}",
            UsernamePatternType.LastFirst => $"{last}{f}@{domain}",
            _ => $"{first}.{last}@{domain}"
        };
    }

    private static string GeneratePersonalEmail(Faker faker, string firstName, string lastName, int index)
    {
        var domain = PersonalEmailDomains[index % PersonalEmailDomains.Length];
        var style = index % 5;

        return style switch
        {
            0 => $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}{faker.Random.Int(1, 99)}@{domain}",
            1 => $"{firstName.ToLowerInvariant()}{faker.Random.Int(1970, 2005)}@{domain}",
            2 => $"{char.ToLowerInvariant(firstName[0])}{lastName.ToLowerInvariant()}{faker.Random.Int(1, 999)}@{domain}",
            3 => $"{lastName.ToLowerInvariant()}.{firstName.ToLowerInvariant()}@{domain}",
            _ => $"{firstName.ToLowerInvariant()}_{faker.Random.Int(100, 9999)}@{domain}"
        };
    }

    private static string GenerateSocialHandle(Faker faker, string firstName, string lastName, int index)
    {
        var prefix = SocialPlatformPrefixes[index % SocialPlatformPrefixes.Length];
        var style = index % 6;

        var handle = style switch
        {
            0 => $"{firstName.ToLowerInvariant()}_{lastName.ToLowerInvariant()}",
            1 => $"{firstName.ToLowerInvariant()}{faker.Random.Int(1, 999)}",
            2 => $"{char.ToLowerInvariant(firstName[0])}{lastName.ToLowerInvariant()}",
            3 => $"{firstName.ToLowerInvariant()}_{faker.Random.Int(10, 99)}",
            4 => $"the_{firstName.ToLowerInvariant()}",
            _ => $"{lastName.ToLowerInvariant()}{char.ToLowerInvariant(firstName[0])}{faker.Random.Int(1, 99)}"
        };

        return $"{prefix}{handle}";
    }

    private static string GenerateUsernameOnly(Faker faker, string firstName, string lastName, int index)
    {
        var style = index % 5;

        return style switch
        {
            0 => $"{firstName.ToLowerInvariant()}{lastName.ToLowerInvariant()}",
            1 => $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}",
            2 => $"{char.ToLowerInvariant(firstName[0])}{lastName.ToLowerInvariant()}{faker.Random.Int(1, 99)}",
            3 => $"{firstName.ToLowerInvariant()}{faker.Random.Int(1980, 2010)}",
            _ => $"{lastName.ToLowerInvariant()}_{firstName.ToLowerInvariant()}"
        };
    }

    private static string GenerateEmployeeId(Faker faker, int index)
    {
        var style = index % 4;

        return style switch
        {
            0 => $"EMP{100000 + index:D6}",
            1 => $"E-{faker.Random.Int(10000, 99999)}",
            2 => $"USR{faker.Random.Int(10000, 99999):D5}",
            _ => $"{faker.Random.AlphaNumeric(2).ToUpperInvariant()}{faker.Random.Int(1000, 9999)}"
        };
    }

    private static string GeneratePhoneNumber(Faker faker, int index)
    {
        // No + prefix per requirements
        var areaCode = 200 + (index % 800);  // Valid US area codes start at 200
        var exchange = faker.Random.Int(200, 999);
        var subscriber = faker.Random.Int(1000, 9999);

        return $"1{areaCode}{exchange}{subscriber}";
    }

    private static string GenerateLegacySystem(string firstName, string lastName, int index)
    {
        var style = index % 4;

        return style switch
        {
            0 => $"{lastName.ToUpperInvariant()[..Math.Min(6, lastName.Length)]}{char.ToUpperInvariant(firstName[0])}{(index % 100):D2}",
            1 => $"{char.ToUpperInvariant(firstName[0])}{lastName.ToUpperInvariant()[..Math.Min(7, lastName.Length)]}",
            2 => $"{lastName.ToUpperInvariant()[..Math.Min(4, lastName.Length)]}{firstName.ToUpperInvariant()[..Math.Min(2, firstName.Length)]}{index % 10}",
            _ => $"U{(10000 + index):D5}"
        };
    }

    private static string GenerateRandomAlphanumeric(Faker faker)
    {
        return faker.Random.AlphaNumeric(10);
    }

    private int GetDeterministicOffset(int index)
    {
        unchecked
        {
            var hash = _seed;
            hash = hash * 397 ^ index;
            return ((hash % 10) + 10) % 10;
        }
    }

    private static string MapRegionToLocale(GeographicRegion region, int seed) => region switch
    {
        GeographicRegion.NorthAmerica => "en_US",
        GeographicRegion.Europe => PickLocale(EuropeanLocales, seed),
        GeographicRegion.AsiaPacific => PickLocale(AsianLocales, seed),
        GeographicRegion.LatinAmerica => PickLocale(LatinAmericanLocales, seed),
        GeographicRegion.MiddleEast => PickLocale(MiddleEastLocales, seed),
        GeographicRegion.Africa => PickLocale(AfricanLocales, seed),
        GeographicRegion.Global => "en",
        _ => "en"
    };

    private static string PickLocale(string[] locales, int seed)
    {
        var length = locales.Length;
        var index = ((seed % length) + length) % length;
        return locales[index];
    }
}
