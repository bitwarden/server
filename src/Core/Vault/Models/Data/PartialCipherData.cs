// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json;
using Bit.Core.Utilities;
using Bit.Core.Vault.Enums;

namespace Bit.Core.Vault.Models.Data;

/// <summary>
/// Produces a "partial" version of a cipher's encrypted <c>Data</c> blob for PAM credential leasing.
/// When a user can only reach a cipher through leasing-enabled collections, sync delivers this reduced
/// blob instead of the full one.
/// </summary>
/// <remarks>
/// Zero-knowledge is preserved: nothing is ever decrypted. This only reshapes the plaintext JSON
/// envelope, keeping the encrypted title (and, for logins, the encrypted URIs) and dropping every other
/// encrypted field (username, password, TOTP, notes, custom fields, etc.). The retained values remain
/// individually-encrypted <c>EncString</c>s.
/// </remarks>
public static class PartialCipherData
{
    /// <summary>
    /// Reduces a cipher's JSON <c>Data</c> blob to the fields allowed under credential leasing.
    /// Logins keep <c>Name</c> and <c>Uris</c>; all other types keep only <c>Name</c>.
    /// </summary>
    /// <param name="type">The cipher's type.</param>
    /// <param name="data">The full, encrypted JSON data blob. Must be JSON (not an SDK-encrypted blob).</param>
    /// <returns>A reduced JSON data blob, or the input unchanged when it is null/empty.</returns>
    public static string Strip(CipherType type, string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return data;
        }

        if (type == CipherType.Login)
        {
            var login = JsonSerializer.Deserialize<CipherLoginData>(data);
            var partial = new CipherLoginData
            {
                Name = login.Name,
                Uris = login.Uris,
            };
            return JsonSerializer.Serialize(partial, JsonHelpers.IgnoreWritingNull);
        }

        var nameOnly = JsonSerializer.Deserialize<NameOnlyData>(data);
        return JsonSerializer.Serialize(
            new NameOnlyData { Name = nameOnly.Name }, JsonHelpers.IgnoreWritingNull);
    }

    private class NameOnlyData
    {
        public string Name { get; set; }
    }
}
