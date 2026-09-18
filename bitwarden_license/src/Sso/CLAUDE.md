# Bitwarden SSO - Claude Code Configuration

## SAML2.0: Verify Sustainsys.Saml2 Behavior From the Package

`Sso.csproj` pins `Sustainsys.Saml2.AspNetCore2` to one exact version. GitHub source and web documentation may describe a different version. Inspect the exact pinned package before it states a claim about the library's real behavior.

## SAML2.0 Security-Critical Invariants

### An EncryptedAssertion can hold a validly signed assertion

- The SAML 2.0 protocol schema defines one repeatable choice group for each assertion entry inside a Response. See the `ResponseType` definition in the official schema: <https://docs.oasis-open.org/security/saml/v2.0/saml-schema-protocol-2.0.xsd>.
- Each entry in that choice is one plain `<Assertion>` element or one `<EncryptedAssertion>` element. A single entry is never both at once. A Response may repeat this choice zero or more times, in any mix of the two element types.
- An Identity Provider (IdP) may sign an assertion and place it only inside an `<EncryptedAssertion>` entry, with no separate plain copy anywhere in the Response. Check both `<Assertion>` and `<EncryptedAssertion>` entries when inspecting Response assertions.

### A collection can carry more than one relevant child

A response may carry more than one assertion. An `<EncryptedAssertion>` may carry more than one `<xenc:EncryptedKey>` element, each with a different algorithm. The XML Encryption spec allows this. Enumerate every child in any collection.

## Certificate Rotation

### Signing-key rotation and encryption-key rotation carry different risk windows

- A signature verifier, the IdP, must trust a new signing key before the signer switches to it.
- Only one signing key is active at a time.
- A decryptor, Bitwarden, can hold more than one valid decryption key at the same time.
- Decryption misalignment risk arrives only when Bitwarden removes the final old key, not when Bitwarden adds a new key.
