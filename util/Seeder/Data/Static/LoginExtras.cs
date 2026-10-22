using System.Text;
using Bit.Core.Vault.Enums;
using Bit.Seeder.Models;

namespace Bit.Seeder.Data.Static;

/// <summary>
/// Deterministic notes, custom fields and TOTP secrets for generated logins.
/// Every method is a pure function of its arguments, so parallel generation steps can call it without coordination.
/// </summary>
internal static class LoginExtras
{
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
    /// <param name="index">Position of the login; picks the length tier and the starting line.</param>
    /// <param name="team">Replaces the <c>{team}</c> placeholder in the note lines.</param>
    internal static string Notes(int index, string team)
    {
        var tier = (index * 29 + 3) % 20;
        var targetLength = tier switch
        {
            < 14 => 30 + (index % 80),
            < 19 => 200 + (index % 300),
            _ => 1_800 + (index % 2_200)
        };

        var text = new StringBuilder(targetLength + 120);
        for (var line = 0; text.Length < targetLength; line++)
        {
            text.Append(_noteLines[(index + line) % _noteLines.Length].Replace("{team}", team)).Append('\n');
        }
        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// Builds one to three custom fields for the login at <paramref name="index"/>.
    /// The last field is hidden; the others are text.
    /// </summary>
    internal static List<FieldViewDto> Fields(int index)
    {
        var count = 1 + (index % 3);
        return Enumerable.Range(0, count)
            .Select(i => new FieldViewDto
            {
                Name = _fieldNames[(index + i) % _fieldNames.Length],
                Value = $"{((uint)index * 7919u + (uint)i * 104_729u) % 100_000_000u:D8}",
                Type = i == count - 1 ? (int)FieldType.Hidden : (int)FieldType.Text
            })
            .ToList();
    }

    /// <summary>
    /// Builds a 32-character base32 TOTP secret for the login at <paramref name="index"/>.
    /// </summary>
    internal static string TotpSecret(int index)
    {
        var secret = new char[32];
        var state = (uint)index * 2_654_435_761u + 1;
        for (var i = 0; i < secret.Length; i++)
        {
            state = state * 1_664_525u + 1_013_904_223u;
            secret[i] = Base32Alphabet[(int)(state >> 27)];
        }
        return new string(secret);
    }
}
