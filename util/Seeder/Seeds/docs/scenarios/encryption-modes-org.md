# I need a shared org vault covering every encryption mode across lifecycle states

> Testing encryption in a shared org vault means covering the cipher-encryption and attachment schemes across items in every lifecycle state — active, archived, and soft-deleted.

## Quick start

```bash
dotnet run -- preset --name qa.paper-trail-partners-team
```

## What you get

A Teams-plan org (**Paper Trail Partners**) with an owner, admin, and member in a single **All-Access** group that reaches every collection — so the owner sees the whole vault. It seeds the **same `encryption-modes` fixture** as the individual preset — 34 ciphers across all eight cipher types — round-robin assigned to ten collections, covering:

- Both cipher-encryption modes (user-key and cipher-key), on ciphers with and without attachments.
- Every attachment scheme (v0/v1/v2), with bodies matched to their host cipher.
- Archived and soft-deleted items (some with attachments), a couple both; the rest active.

Archived/deleted dates are backdated. Attachment bodies are the `mock-seeder-data-*` files under `Seeds/attachments/`. Log in as `trail.owner@papertrail.example` with password `asdfasdfasdf`.

## Variations

For the personal-vault equivalent, see [encryption-modes.md](encryption-modes.md). Add `--mangle` to seed more than once without colliding.

For CLI flags, see the [SeederUtility reference](../../../../SeederUtility/README.md). For the full preset catalog, see [presets.md](../presets.md).
