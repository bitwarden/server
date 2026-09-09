namespace Bit.Seeder.Enums;

/// <summary>
/// How an attachment's file bytes and metadata are encrypted, using Bitwarden's canonical v0/v1/v2
/// attachment scheme names so clients exercise every attachment decrypt branch. The enum value is the
/// version number and is passed verbatim across the Rust FFI (see <c>RustSdkService.EncryptAttachment</c>).
///
/// Note: this is the *attachment* scheme version. It is unrelated to account "Encryption V1/V2" (the
/// AES-CBC-HMAC → XChaCha20/COSE overhaul). Every scheme here emits Encryption-V1 type-2 EncStrings.
/// </summary>
internal enum AttachmentSchemeType
{
    /// <summary>
    /// v0 (account-key-based, legacy): no attachment key. File bytes and filename are encrypted directly
    /// with the vault key; the attachment metadata carries a null <c>Key</c>.
    /// </summary>
    V0 = 0,

    /// <summary>
    /// v1 (attachment-key-based): file bytes encrypted with a per-attachment key that is wrapped by the
    /// vault key; the filename is encrypted with the vault key.
    /// </summary>
    V1 = 1,

    /// <summary>
    /// v2 (attachment-cipher-key-based, modern): file bytes encrypted with a per-attachment key that is
    /// wrapped by the cipher key; the filename is encrypted with the cipher key. Requires the host cipher
    /// to use <see cref="CipherEncryptionType.CipherKey"/>.
    /// </summary>
    V2 = 2,
}
