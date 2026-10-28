using Bit.Seeder.Data.Generators;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Data;

public class PerIndexSeedingTests
{
    // Seeding each index with seed + index makes (seed, index + offset) and (seed + offset, index) the same stream,
    // so a second org at a nearby seed would repeat the first org's items shifted by the offset
    [Theory]
    [InlineData("Card", 1, 10)]
    [InlineData("Identity", 1, 10)]
    // 15 is the number of note categories, so both sides share a category; indices 6 and 7 (WiFi, Database) are
    // skipped because Bogus's password generator can loop forever there
    [InlineData("SecureNote", 15, 5)]
    [InlineData("LoginNotes", 1, 10)]
    [InlineData("CustomField", 1, 10)]
    [InlineData("TotpSecret", 1, 10)]
    public void GenerateByIndex_SeedAndIndexTradedOffAgainstEachOther_GiveDifferentOutput(string generator, int offset, int count)
    {
        const int seed = 42;

        var indexShifted = Enumerable.Range(0, count).Select(index => Output(generator, seed, index + offset));
        var seedShifted = Enumerable.Range(0, count).Select(index => Output(generator, seed + offset, index));

        Assert.NotEqual(string.Join('\u001f', indexShifted), string.Join('\u001f', seedShifted));
    }

    // Only the part of each output that comes from the randomizer: a card's brand and expiry come from the index
    private static string Output(string generator, int seed, int index)
    {
        switch (generator)
        {
            case "Card":
                return new CardDataGenerator(seed).GenerateByIndex(index).CardholderName!;
            case "Identity":
                var identity = new IdentityDataGenerator(seed).GenerateByIndex(index);
                return $"{identity.FirstName} {identity.LastName}";
            case "SecureNote":
                var (name, notes) = new SecureNoteDataGenerator(seed).GenerateByIndex(index);
                return $"{name}\n{notes}";
            case "LoginNotes":
                // Rank 21 of 22 is the runbook-sized tier on both sides, so only the randomizer decides the text
                return new LoginNotesGenerator(seed).GenerateByIndex(index, 21, 22, "Engineering");
            case "CustomField":
                return string.Join(',', new CustomFieldGenerator(seed).GenerateByIndex(index).Select(field => $"{field.Name}={field.Value}"));
            case "TotpSecret":
                return new TotpSecretGenerator(seed).GenerateByIndex(index);
            default:
                throw new ArgumentOutOfRangeException(nameof(generator), generator, null);
        }
    }
}
