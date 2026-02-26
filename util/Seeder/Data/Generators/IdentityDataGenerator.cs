using Bit.Seeder.Data.Enums;
using Bit.Seeder.Models;
using Bogus;

namespace Bit.Seeder.Data.Generators;

internal sealed class IdentityDataGenerator(int seed, GeographicRegion region = GeographicRegion.Global)
{
    private readonly int _seed = seed;

    private readonly GeographicRegion _region = region;

    private static readonly Dictionary<GeographicRegion, string[]> _regionalTitles = new()
    {
        [GeographicRegion.NorthAmerica] = ["Mr", "Mrs", "Ms", "Dr", "Prof"],
        [GeographicRegion.Europe] = ["Mr", "Mrs", "Ms", "Dr", "Prof", "Sir", "Dame"],
        [GeographicRegion.AsiaPacific] = ["Mr", "Mrs", "Ms", "Dr"],
        [GeographicRegion.LatinAmerica] = ["Sr", "Sra", "Srta", "Dr", "Prof"],
        [GeographicRegion.MiddleEast] = ["Mr", "Mrs", "Ms", "Dr", "Sheikh", "Sheikha"],
        [GeographicRegion.Africa] = ["Mr", "Mrs", "Ms", "Dr", "Chief"],
        [GeographicRegion.Global] = ["Mr", "Mrs", "Ms", "Dr", "Prof"]
    };

    /// <summary>
    /// Generates a deterministic identity based on index.
    /// </summary>
    internal IdentityViewDto GenerateByIndex(int index)
    {
        var seededFaker = new Faker(MapRegionToLocale(_region)) { Random = new Randomizer(_seed + index) };
        var person = seededFaker.Person;
        var titles = _regionalTitles[_region];

        return new IdentityViewDto
        {
            Title = titles[index % titles.Length],
            FirstName = person.FirstName,
            MiddleName = index % 3 == 0 ? seededFaker.Name.FirstName() : null,
            LastName = person.LastName,
            Address1 = seededFaker.Address.StreetAddress(),
            Address2 = index % 5 == 0 ? seededFaker.Address.SecondaryAddress() : null,
            Address3 = null,
            City = seededFaker.Address.City(),
            State = seededFaker.Address.StateAbbr(),
            PostalCode = seededFaker.Address.ZipCode(),
            Country = GetCountryCode(seededFaker),
            Company = index % 2 == 0 ? seededFaker.Company.CompanyName() : null,
            Email = person.Email,
            Phone = seededFaker.Phone.PhoneNumber(),
            SSN = GenerateNationalIdByIndex(index),
            Username = person.UserName,
            PassportNumber = index % 3 == 0 ? GeneratePassportNumberByIndex(index) : null,
            LicenseNumber = index % 2 == 0 ? GenerateLicenseNumberByIndex(index) : null
        };
    }

    private string GenerateNationalIdByIndex(int index) => _region switch
    {
        GeographicRegion.NorthAmerica => $"{100 + (index % 899):D3}-{10 + (index % 90):D2}-{1000 + (index % 9000):D4}",
        GeographicRegion.Europe => $"AB {10 + (index % 90):D2} {10 + ((index + 1) % 90):D2} {10 + ((index + 2) % 90):D2} C",
        GeographicRegion.AsiaPacific => $"{1000 + (index % 9000):D4}-{1000 + ((index + 1) % 9000):D4}-{1000 + ((index + 2) % 9000):D4}",
        GeographicRegion.LatinAmerica => $"{100 + (index % 900):D3}.{100 + ((index + 1) % 900):D3}.{100 + ((index + 2) % 900):D3}-{10 + (index % 90):D2}",
        _ => $"{100 + (index % 899):D3}-{10 + (index % 90):D2}-{1000 + (index % 9000):D4}"
    };

    private static string GeneratePassportNumberByIndex(int index) =>
        $"{(char)('A' + index % 26)}{10000000 + index}";

    private static string GenerateLicenseNumberByIndex(int index) =>
        $"DL{1000000 + index}";

    private string GetCountryCode(Faker faker) => _region switch
    {
        GeographicRegion.NorthAmerica => faker.PickRandom("US", "CA"),
        GeographicRegion.Europe => faker.PickRandom("GB", "DE", "FR", "ES", "IT", "NL"),
        GeographicRegion.AsiaPacific => faker.PickRandom("JP", "CN", "IN", "AU", "KR", "SG"),
        GeographicRegion.LatinAmerica => faker.PickRandom("BR", "MX", "AR", "CO", "CL"),
        GeographicRegion.MiddleEast => faker.PickRandom("AE", "SA", "IL", "TR"),
        GeographicRegion.Africa => faker.PickRandom("ZA", "NG", "EG", "KE"),
        _ => faker.Address.CountryCode()
    };

    private static string MapRegionToLocale(GeographicRegion region) => region switch
    {
        GeographicRegion.NorthAmerica => "en_US",
        GeographicRegion.Europe => "en_GB",
        GeographicRegion.AsiaPacific => "en",
        GeographicRegion.LatinAmerica => "es",
        GeographicRegion.MiddleEast => "en",
        GeographicRegion.Africa => "en",
        _ => "en"
    };
}
