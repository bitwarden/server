// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json;
using Bit.Core.Utilities;
using Bit.Core.Vault.Enums;

namespace Bit.Core.Vault.Models.Data;

/// <summary>
/// The "partial" version of a cipher's encrypted <c>Data</c> blob for PAM credential leasing. When a user
/// can only reach a cipher through leasing-enabled collections, they receive this reduced blob instead of
/// the full one. The properties declared here are the entire allowlist: everything else in the cipher's
/// data is dropped.
/// </summary>
/// <remarks>
/// Zero-knowledge is preserved: nothing is ever decrypted. This only reshapes the plaintext JSON
/// envelope, keeping the encrypted title (and, for logins, the encrypted URIs) and dropping every other
/// encrypted field (username, password, TOTP, notes, custom fields, etc.). The retained values remain
/// individually-encrypted <c>EncString</c>s.
///
/// Deliberately not a reduced <see cref="CipherLoginData"/>: that would carry its base-class fields and
/// its legacy computed singular <c>Uri</c> getter into the envelope.
/// </remarks>
public class PartialCipherData
{
    public string Name { get; set; }

    public IEnumerable<CipherLoginData.CipherLoginUriData> Uris { get; set; }

    /// <summary>
    /// Reduces a cipher's JSON <c>Data</c> blob to the fields allowed under credential leasing.
    /// Logins keep <c>Name</c> and <c>Uris</c>; all other types keep only <c>Name</c>.
    /// </summary>
    /// <param name="type">The cipher's type.</param>
    /// <param name="data">The full, encrypted JSON data blob. Must be JSON (not an SDK-encrypted blob).</param>
    /// <returns>
    /// A reduced JSON data blob, or the input unchanged when it is null/empty. The output is a
    /// <b>camelCase</b> envelope (<c>name</c>, and for logins <c>uris</c>: <c>uri</c>,
    /// <c>uriChecksum</c>, <c>match</c>) — the shape the SDK's restricted decrypt path consumes,
    /// matching how it deserializes a full login's URIs. Input is parsed case-insensitively so the
    /// stored PascalCase blob and an already-stripped camelCase blob both round-trip (idempotent).
    /// </returns>
    public static string Strip(CipherType type, string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return data;
        }

        var partial = JsonSerializer.Deserialize<PartialCipherData>(data, JsonHelpers.IgnoreCase);

        if (type != CipherType.Login)
        {
            // Not conditional on the input containing URIs: a strip must not depend on the shape of the
            // blob it is handed.
            partial.Uris = null;
        }

        return JsonSerializer.Serialize(partial, JsonHelpers.IgnoreWritingNullAndCamelCase);
    }
}
