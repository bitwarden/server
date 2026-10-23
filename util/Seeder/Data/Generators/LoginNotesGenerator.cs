using System.Text;
using Bit.Seeder.Data.Static;
using Bogus;

namespace Bit.Seeder.Data.Generators;

/// <summary>
/// Deterministic notes for generated logins.
/// </summary>
internal sealed class LoginNotesGenerator(int seed)
{
    private static readonly ThreadLocal<Faker> _threadFaker = new(() => new Faker());

    /// <summary>
    /// Builds a note for the login at <paramref name="index"/>, with a long tail of lengths:
    /// 70% one or two lines, 25% a paragraph, 5% a runbook-sized note.
    /// </summary>
    /// <param name="index">Position of the login; seeds the length within its tier and the starting line.</param>
    /// <param name="rank">Position of the login among the note-bearing logins, from 0 to <paramref name="rankCount"/> - 1; picks the length tier.</param>
    /// <param name="rankCount">How many ranks there are.</param>
    /// <param name="team">Replaces the <c>{team}</c> placeholder in the note lines.</param>
    internal string GenerateByIndex(int index, int rank, int rankCount, string team)
    {
        var faker = _threadFaker.Value!;
        faker.Random = new Randomizer(ItemSeedMixer.Mix(seed, index));

        // The midpoint of this login's slice of the note-bearing logins, as a percentile
        var percentile = (rank * 100 + 50) / rankCount;
        var targetLength = percentile switch
        {
            < 70 => faker.Random.Number(30, 109),
            < 95 => faker.Random.Number(200, 499),
            _ => faker.Random.Number(1_800, 3_999)
        };
        var startLine = faker.Random.Number(0, NoteLines.All.Length - 1);

        var text = new StringBuilder(targetLength + 120);
        for (var line = 0; text.Length < targetLength; line++)
        {
            text.Append(NoteLines.All[(startLine + line) % NoteLines.All.Length].Replace("{team}", team)).Append('\n');
        }
        return text.ToString().TrimEnd();
    }
}
