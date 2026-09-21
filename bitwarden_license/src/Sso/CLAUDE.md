# Bitwarden SSO - Claude Code Configuration

## SAML2.0: Verify Sustainsys.Saml2 Behavior From the Package

`Sso.csproj` pins `Sustainsys.Saml2.AspNetCore2` to one exact version. GitHub source and web documentation may describe a different version. Inspect the exact pinned package before stating a claim about the library's real behavior.

## Certificate Rotation

### Signing-key rotation and encryption-key rotation carry different risk windows

- A signature verifier, the IdP, must trust a new signing key before the signer switches to it.
- Only one signing key is active at a time.
- A decryptor, Bitwarden, can hold more than one valid decryption key at the same time.
- Decryption misalignment risk arrives only when Bitwarden removes the final old key, not when Bitwarden adds a new key.
