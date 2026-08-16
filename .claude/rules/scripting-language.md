---
paths:
  - "dev/**"
  - "**/*.ps1"
---

# Developer tooling: PowerShell by default

New cross-platform developer tooling under `dev/` is written in PowerShell (`.ps1`) — see `dev/migrate.ps1`,
`dev/ef_migrate.ps1`, `dev/seed.ps1`. This keeps a single command set working on Windows, macOS, and Linux
contributor machines and matches how the repo's setup docs invoke these scripts (`pwsh dev/migrate.ps1`).

Do **not** proliferate a pattern of scripting pairs - both shell and Powershell to accomplish the same task.

This rule does not retroactively apply to existing scripts. These stay in shell:

- Container `entrypoint.sh` / `build.sh` in each service (`src/Api`, `src/Identity`, `src/Admin`,
  `bitwarden_license/src/Sso`, `util/MsSql`, `util/Setup`, …). `pwsh` is not present in those runtime images.
- `.devcontainer/` provisioning scripts.
- Per-platform pairs where the sibling `.ps1` already exists (`dev/create_certificates_{linux,mac}.sh` alongside
  `create_certificates_windows.ps1`).
- `run:` steps in GitHub Actions workflows.

When adding any new scripts to this repository, use PowerShell. When touching one of the exempted paths above, match the
existing language.
