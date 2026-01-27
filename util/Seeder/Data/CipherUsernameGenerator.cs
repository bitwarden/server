using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Data;

/// <summary>
/// Generates deterministic usernames for companies using configurable patterns.
/// Uses Bogus library for locale-aware name generation while maintaining determinism
/// through pre-generated arrays indexed by a seed.
/// </summary>
internal sealed class CipherUsernameGenerator
{
    private const int _namePoolSize = 1500;

    private readonly Random _random;

    private readonly UsernamePattern _pattern;

    private readonly string[] _firstNames;

    private readonly string[] _lastNames;

    public CipherUsernameGenerator(
        int seed,
        UsernamePatternType patternType = UsernamePatternType.FirstDotLast,
        GeographicRegion? region = null)
    {
        _random = new Random(seed);
        _pattern = UsernamePatterns.GetPattern(patternType);

        // Pre-generate arrays from Bogus for deterministic index-based access
        var provider = new BogusNameProvider(region ?? GeographicRegion.Global, seed);
        _firstNames = Enumerable.Range(0, _namePoolSize).Select(_ => provider.FirstName()).ToArray();
        _lastNames = Enumerable.Range(0, _namePoolSize).Select(_ => provider.LastName()).ToArray();
    }

    public string Generate(Company company)
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        return _pattern.Generate(firstName, lastName, company.Domain);
    }

    /// <summary>
    /// Generates username using index for deterministic selection across cipher iterations.
    /// </summary>
    public string GenerateByIndex(Company company, int index)
    {
        var firstName = _firstNames[index % _firstNames.Length];
        var lastName = _lastNames[(index * 7) % _lastNames.Length]; // Prime multiplier for variety
        return _pattern.Generate(firstName, lastName, company.Domain);
    }

    /// <summary>
    /// Combines deterministic index with random offset for controlled variety.
    /// </summary>
    public string GenerateVaried(Company company, int index)
    {
        var offset = _random.Next(10);
        var firstName = _firstNames[(index + offset) % _firstNames.Length];
        var lastName = _lastNames[(index * 7 + offset) % _lastNames.Length];
        return _pattern.Generate(firstName, lastName, company.Domain);
    }

    public string GetFirstName(int index) => _firstNames[index % _firstNames.Length];

    public string GetLastName(int index) => _lastNames[(index * 7) % _lastNames.Length];
}
