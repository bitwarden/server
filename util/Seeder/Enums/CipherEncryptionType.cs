namespace Bit.Seeder.Enums;

/// <summary>
/// How a cipher's fields are encrypted. Bitwarden's canonical terms are "User Key encryption" and
/// "Cipher Key encryption"; a cipher and its attachments must use the same strategy (see
/// <see cref="AttachmentSchemeType"/>).
/// </summary>
internal enum CipherEncryptionType
{
    /// <summary>
    /// Fields encrypted directly with the vault (user or organization) key; no per-cipher key.
    /// The resulting <c>Cipher.Key</c> is null. The original scheme.
    /// </summary>
    UserKey,

    /// <summary>
    /// Fields encrypted with a freshly generated per-cipher key that is wrapped by the vault key and
    /// stored on <c>Cipher.Key</c>. Required to host a <see cref="AttachmentSchemeType.V2"/> attachment.
    /// </summary>
    CipherKey,
}
