# Identity and SSO certificates

The server uses three certificate roles. Each role can rotate to a new certificate without downtime and without a code release. This document describes the settings, the rotation path, and the rules that keep users signed in during a rotation.

## The three roles

| Role | Host | What the certificate does | Settings section |
|---|---|---|---|
| Identity signing | `/identity` | Signs access tokens. | `globalSettings:identityServer` |
| SSO signing | `/sso` | Signs SSO OIDC tokens, SAML requests, and SAML metadata. | `globalSettings:ssoServer` |
| SSO encryption | `/sso` | Decrypts SAML assertions. SAML metadata publishes it. | `globalSettings:ssoServer:encryption` |

Each certificate must have an RSA key and its private key, in a password-protected PKCS#12 (`.pfx`) file.

## Where a certificate loads from

Every certificate reference has the same four fields. The host uses the first source that applies:

1. `certificateLocation` and `certificatePassword`: a file. Self-hosted only, and the file must exist.
2. `certificateThumbprint`: the `CurrentUser\My` certificate store. Typical for local development.
3. `blobName` and `certificatePassword`: a blob in the `certificates` container of `globalSettings:storage:connectionString`. Cloud only.

## Settings for one role

Each role has three slots:

| Slot | Purpose | Signing roles | SSO encryption |
|---|---|---|---|
| Root fields | The active certificate. | Signs. Published. | Decrypts. Published in SAML metadata. |
| `futureCertificate` | The next certificate. | Published. Signs after promotion. | Decrypts. Not published until promotion. |
| `validationCertificates` (signing) or `decryptionCertificates` (encryption) | Outgoing certificates. | Published. Never signs. | Decrypts. Never published. |

"Published" means the key is in the JWKS, so token consumers can validate it. The SAML metadata also lists the active and future SSO signing certificates.

Two flags control the slots:

| Flag | On | Default | Effect |
|---|---|---|---|
| `enabled` | `futureCertificate` and each list entry | `false` for `futureCertificate`, `true` for a list entry | The host loads the certificate. If it does not load, the host does not start. |
| `active` | `futureCertificate` | `false` | With `enabled`, the future certificate becomes the active certificate. The host then ignores the root fields. |

A cloud example, with SSO encryption in the middle of a rotation:

```json
{
  "globalSettings": {
    "ssoServer": {
      "blobName": "sso-signing.pfx",
      "certificatePassword": "<secret>",
      "encryption": {
        "blobName": "sso-encryption-a.pfx",
        "certificatePassword": "<secret>",
        "futureCertificate": {
          "blobName": "sso-encryption-b.pfx",
          "certificatePassword": "<secret>",
          "enabled": true,
          "active": false
        },
        "decryptionCertificates": []
      }
    }
  }
}
```

As environment variables, each level is a double underscore, and list entries use an index:

```
globalSettings__ssoServer__encryption__futureCertificate__enabled=true
globalSettings__identityServer__validationCertificates__0__blobName=identity-a.pfx
```

A self-hosted host uses `certificateLocation` in place of `blobName`.

## Rotating a certificate

A certificate moves through the slots in this order: `futureCertificate`, then the root fields, then the list, then out.

| Step | Root | `futureCertificate` | List | Active |
|---|---|---|---|---|
| Before | A | | | A |
| 1. Stage | A | B, `enabled` | | A |
| 2. Prepare | A | B, `enabled` | A | A |
| 3. Promote | A (ignored) | B, `enabled`, `active` | A | B |
| 4. Clean up | B | | A | B |
| 5. Retire | B | | | B |

Rules that keep each step safe:

- A host reads its certificates only at startup. Each step is a configuration change, then a restart of every instance of the host.
- Set the password before you enable an entry. Disable an entry before you remove its password.
- The lists bind by index. Add entries only at the end. Do not change the index of an enabled entry.
- Wait between Stage and Promote. Each token consumer caches the JWKS. A consumer that still has the old copy rejects the first token signed with B. For SSO, customer identity providers also need time to accept B.
- Wait between Promote and Retire. Tokens signed with A stay valid until they expire, and they need A in the list.

Token consumers include `/api` and the other services that accept `/identity` tokens, and Key Connector. `/identity` is itself a token consumer of `/sso`.

## Rollback

Each rollback is one configuration change and a restart. The outgoing certificate becomes the only active certificate again.

| From | Change |
|---|---|
| Stage or Prepare | In `futureCertificate`, set `enabled = false`. |
| Promote | In `futureCertificate`, set `active = false`. |
| Clean up or later | Set `futureCertificate` to A, with `enabled` and `active`. Add B to the end of the list. |

For SSO encryption, an identity provider may already encrypt to B. Do not set `enabled = false` on B in that case. Add B to the end of `decryptionCertificates` first, then disable `futureCertificate`. B then keeps decrypting, and SAML metadata does not publish it.

Keep the outgoing certificate file or blob until the rotation is complete.

## Self-hosted and local development

A self-hosted or Development `/sso` host can leave an SSO role unset. The role then uses the Identity certificates:

- SSO signing uses the Identity active, staged, and validation certificates.
- SSO encryption uses the Identity active and staged certificates only.

So one `identity.pfx` serves all three roles, and an Identity rotation also rotates SSO. One extra step keeps SAML sign-in working: in the Prepare step, also add A to `ssoServer:encryption:decryptionCertificates`. Without it, SAML sign-in fails after Promote for each identity provider that still encrypts to A.

A cloud `/sso` host must set both SSO roles. If a role is not set, `/sso` does not start.

## At startup

The host stops at startup if a configured certificate cannot be used. Causes:

- An enabled entry that does not load: a missing file, blob, or thumbprint, or a wrong password. The error names the setting path.
- A certificate with no RSA key. The error names the setting path.
- A certificate with no private key, if the host signs with it or SAML uses it. For SAML, the error names the role and the thumbprint. For token signing, the error is the message of the token library.

When the host starts, it logs one line at _information_ severity for each role:

```
{Role} certificate source: {Source}. Active thumbprint: {thumbprint}. Staged thumbprints: {list}. Retired (validation or decryption) thumbprints: {list}.
```

`{Source}` is `IdentityServer`, `SsoServer`, or `Identity fallback`. Use this line to confirm each step on every instance.
