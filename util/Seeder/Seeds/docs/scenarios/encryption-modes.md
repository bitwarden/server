# I need ciphers and attachments encrypted the way older and newer clients wrote them

> Vault data written by different client eras decrypts through different key paths — user-key vs. cipher-key ciphers, and v0/v1/v2 attachment schemes.

## Quick start

```bash
dotnet run -- preset --name individual.encryption-modes
```

## What you get

A premium individual account — log in as `encryptionmodes@individual.example` with password `asdfasdfasdf`. Its vault holds 34 ciphers across all eight cipher types (login, card, identity, secure note, SSH key, bank account, driver's license, passport), covering:

- **Cipher encryption** — user-key (`Cipher.Key` null) and cipher-key (per-cipher key wrapped by the vault key), on ciphers both with and without attachments.
- **Attachment schemes** — v0 (no attachment key), v1 (key wrapped by the user key), v2 (key wrapped by the cipher key), plus a cipher carrying both a v0 and a v1 attachment.
- **Lifecycle** — some items archived, some soft-deleted, a couple both; some still carry attachments.
- **Controls** — a user-key and a cipher-key cipher, each with no attachment.

The same fixture is seeded into an org vault by `qa.paper-trail-partners-team`. Attachment bodies are bundled `mock-seeder-data-*` `.txt`/`.pdf` files under `Seeds/attachments/`; blobs decrypt end-to-end.

## Prerequisites

Attachment blobs are written to `{globalSettings:attachment:baseDirectory}/{cipherId}/{attachmentId}`. The SeederUtility must resolve a **writable** `globalSettings:attachment:baseDirectory`, and for a client to download them it must be the **same** directory the running dev API reads from.

## Variations

For the shared org-vault equivalent, see [encryption-modes-org.md](encryption-modes-org.md). Add `--mangle` to seed more than once without colliding.

For CLI flags, see the [SeederUtility reference](../../../../SeederUtility/README.md). For the full preset catalog, see [presets.md](../presets.md).
