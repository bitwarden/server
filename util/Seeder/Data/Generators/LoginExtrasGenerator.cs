using System.Text;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Models;
using Bogus;

namespace Bit.Seeder.Data.Generators;

/// <summary>
/// Deterministic notes, custom fields and TOTP secrets for generated logins.
/// </summary>
internal sealed class LoginExtrasGenerator(int seed)
{
    private static readonly ThreadLocal<Faker> _threadFaker = new(() => new Faker());

    private readonly int _seed = seed;

    private static readonly string[] _noteLines =
    [
        "Shared account owned by the {team} team. Ask before changing the password.",
        "Renewal is handled by procurement; contract number is in the vendor folder.",
        "MFA device is the team phone kept in the IT cabinet.",
        "Use the SSO button first; this local login is the break-glass fallback.",
        "Rotated after the last access review. Next review is due at quarter end.",
        "Support contact: vendor portal, priority 2. Account manager changes yearly.",
        "Do not use for production changes without a ticket.",
        "Recovery codes were printed and stored with the facilities safe.",
        "Read-only reporting user. Requests for write access go through the {team} lead.",
        "Billing alerts go to the shared mailbox, not to individuals.",
        "Legacy integration still points here; migration is tracked in the backlog.",
        "Security questions are set to random values stored in the custom fields."
    ];

    private static readonly string[] _fieldNames = ["Account ID", "Tenant", "Customer number", "PIN", "Region", "API key"];

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    /// Builds a note for the login at <paramref name="index"/>, with a long tail of lengths:
    /// 70% one or two lines, 25% a paragraph, 5% a runbook-sized note.
    /// </summary>
    /// <param name="index">Position of the login; seeds the length within its tier and the starting line.</param>
    /// <param name="rank">Position of the login among the note-bearing logins, from 0 to <paramref name="rankCount"/> - 1; picks the length tier.</param>
    /// <param name="rankCount">How many ranks there are.</param>
    /// <param name="team">Replaces the <c>{team}</c> placeholder in the note lines.</param>
    internal string GenerateNotes(int index, int rank, int rankCount, string team)
    {
        var faker = _threadFaker.Value!;
        faker.Random = new Randomizer(_seed + index);

        // The midpoint of this login's slice of the note-bearing logins, as a percentile
        var percentile = (rank * 100 + 50) / rankCount;
        var targetLength = percentile switch
        {
            < 70 => faker.Random.Number(30, 109),
            < 95 => faker.Random.Number(200, 499),
            _ => faker.Random.Number(1_800, 3_999)
        };
        var startLine = faker.Random.Number(0, _noteLines.Length - 1);

        var text = new StringBuilder(targetLength + 120);
        for (var line = 0; text.Length < targetLength; line++)
        {
            text.Append(_noteLines[(startLine + line) % _noteLines.Length].Replace("{team}", team)).Append('\n');
        }
        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// Builds one to three custom fields with distinct names for the login at <paramref name="index"/>.
    /// The last field is hidden; the others are text.
    /// </summary>
    internal List<FieldViewDto> GenerateFields(int index)
    {
        var faker = _threadFaker.Value!;
        faker.Random = new Randomizer(_seed + index);

        var names = faker.PickRandom(_fieldNames, faker.Random.Number(1, 3)).ToList();
        return names
            .Select((name, i) => new FieldViewDto
            {
                Name = name,
                Value = faker.Random.ReplaceNumbers("########"),
                Type = i == names.Count - 1 ? (int)FieldType.Hidden : (int)FieldType.Text
            })
            .ToList();
    }

    /// <summary>
    /// Builds a 32-character base32 TOTP secret for the login at <paramref name="index"/>.
    /// </summary>
    internal string GenerateTotpSecret(int index)
    {
        var faker = _threadFaker.Value!;
        faker.Random = new Randomizer(_seed + index);
        return faker.Random.String2(32, Base32Alphabet);
    }
}
