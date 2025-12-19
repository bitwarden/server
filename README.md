<p align="center">
  <img src="https://github.com/bitwarden/brand/blob/main/screenshots/apps-combo-logo.png" alt="Bitwarden" />
</p>
<p align="center">
  <a href="https://github.com/bitwarden/server/actions/workflows/build.yml?query=branch:main" target="_blank">
    <img src="https://github.com/bitwarden/server/actions/workflows/build.yml/badge.svg?branch=main" alt="Github Workflow build on main" />
  </a>
  <a href="https://gitter.im/bitwarden/Lobby" target="_blank">
    <img src="https://badges.gitter.im/bitwarden/Lobby.svg" alt="gitter chat" />
  </a>
</p>

---

The Bitwarden Server project contains the APIs, database, and other core infrastructure items needed for the "backend" of all bitwarden client applications.

The server project is written in C# using .NET Core with ASP.NET Core. The database is written in T-SQL/SQL Server. The codebase can be developed, built, run, and deployed cross-platform on Windows, macOS, and Linux distributions.

## Developer Documentation

Please refer to the [Server Setup Guide](https://contributing.bitwarden.com/getting-started/server/guide) in the [Contributing Documentation](https://contributing.bitwarden.com/) for build instructions, recommended tooling, code style tips, and lots of other great information to get you started.

## Deploy

<p align="center">
  <a href="https://github.com/orgs/bitwarden/packages" target="_blank">
    <img src="https://i.imgur.com/SZc8JnH.png" alt="docker" />
  </a>
</p>

You can deploy Bitwarden using Docker containers on Windows, macOS, and Linux distributions. Use the provided PowerShell and Bash scripts to get started quickly. Find all of the Bitwarden images on [GitHub Container Registry](https://github.com/orgs/bitwarden/packages).

Full documentation for deploying Bitwarden with Docker can be found in our help center at: https://help.bitwarden.com/article/install-on-premise/

### Requirements

- [Docker](https://www.docker.com/community-edition#/download)
- [Docker Compose](https://docs.docker.com/compose/install/) (already included with some Docker installations)

_These dependencies are free to use._

### Linux & macOS

```sh
curl -s -L -o bitwarden.sh \
    "https://func.bitwarden.com/api/dl/?app=self-host&platform=linux" \
    && chmod +x bitwarden.sh
./bitwarden.sh install
./bitwarden.sh start
```

### Windows

```cmd
Invoke-RestMethod -OutFile bitwarden.ps1 `
    -Uri "https://func.bitwarden.com/api/dl/?app=self-host&platform=windows"
.\bitwarden.ps1 -install
.\bitwarden.ps1 -start
```

## Production Container Images

<details>
<summary><b>View Current Production Image Hashes</b> (click to expand)</summary>
<br>

### US Production Cluster

| Service | Image Hash |
|---------|------------|
| **[Admin](https://github.com/bitwarden/self-host/pkgs/container/admin/versions)** | ![admin](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge.json&query=%24.admin&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |
| **[API](https://github.com/bitwarden/self-host/pkgs/container/api/versions)** | ![api](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge.json&query=%24.api&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |
| **[Billing](https://github.com/bitwarden/self-host/pkgs/container/billing/versions)** | ![billing](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge.json&query=%24.billing&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |
| **[Events](https://github.com/bitwarden/self-host/pkgs/container/events/versions)** | ![events](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge.json&query=%24.events&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |
| **[EventsProcessor](https://github.com/bitwarden/self-host/pkgs/container/events-processor/versions)** | ![eventsprocessor](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge.json&query=%24.eventsprocessor&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |
| **[Identity](https://github.com/bitwarden/self-host/pkgs/container/identity/versions)** | ![identity](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge.json&query=%24.identity&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |
| **[Notifications](https://github.com/bitwarden/self-host/pkgs/container/notifications/versions)** | ![notifications](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge.json&query=%24.notifications&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |
| **[SCIM](https://github.com/bitwarden/self-host/pkgs/container/scim/versions)** | ![scim](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge.json&query=%24.scim&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |
| **[SSO](https://github.com/bitwarden/self-host/pkgs/container/sso/versions)** | ![sso](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge.json&query=%24.sso&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |

### EU Production Cluster

| Service | Image Hash |
|---------|------------|
| **[Admin](https://github.com/bitwarden/self-host/pkgs/container/admin/versions)** | ![admin](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge-eu.json&query=%24.admin&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |
| **[API](https://github.com/bitwarden/self-host/pkgs/container/api/versions)** | ![api](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge-eu.json&query=%24.api&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |
| **[Billing](https://github.com/bitwarden/self-host/pkgs/container/billing/versions)** | ![billing](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge-eu.json&query=%24.billing&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |
| **[Events](https://github.com/bitwarden/self-host/pkgs/container/events/versions)** | ![events](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge-eu.json&query=%24.events&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |
| **[EventsProcessor](https://github.com/bitwarden/self-host/pkgs/container/events-processor/versions)** | ![eventsprocessor](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge-eu.json&query=%24.eventsprocessor&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |
| **[Identity](https://github.com/bitwarden/self-host/pkgs/container/identity/versions)** | ![identity](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge-eu.json&query=%24.identity&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |
| **[Notifications](https://github.com/bitwarden/self-host/pkgs/container/notifications/versions)** | ![notifications](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge-eu.json&query=%24.notifications&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |
| **[SCIM](https://github.com/bitwarden/self-host/pkgs/container/scim/versions)** | ![scim](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge-eu.json&query=%24.scim&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |
| **[SSO](https://github.com/bitwarden/self-host/pkgs/container/sso/versions)** | ![sso](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2Fbitwarden%2Fserver%2Frefs%2Fheads%2Fmetadata%2Fbadges%2Fshieldsio-badge-eu.json&query=%24.sso&style=flat-square&logo=docker&logoColor=white&label=&color=2496ED) |

</details>

## We're Hiring!

Interested in contributing in a big way? Consider joining our team! We're hiring for many positions. Please take a look at our [Careers page](https://bitwarden.com/careers/) to see what opportunities are currently open as well as what it's like to work at Bitwarden.

## Contribute

Code contributions are welcome! Please commit any pull requests against the `main` branch. Learn more about how to contribute by reading the [Contributing Guidelines](https://contributing.bitwarden.com/contributing/). Check out the [Contributing Documentation](https://contributing.bitwarden.com/) for how to get started with your first contribution.

Security audits and feedback are welcome. Please open an issue or email us privately if the report is sensitive in nature. You can read our security policy in the [`SECURITY.md`](SECURITY.md) file. We also run a program on [HackerOne](https://hackerone.com/bitwarden).

No grant of any rights in the trademarks, service marks, or logos of Bitwarden is made (except as may be necessary to comply with the notice requirements as applicable), and use of any Bitwarden trademarks must comply with [Bitwarden Trademark Guidelines](https://github.com/bitwarden/server/blob/main/TRADEMARK_GUIDELINES.md).

### Dotnet-format

Consider installing our git pre-commit hook for automatic formatting.

```bash
git config --local core.hooksPath .git-hooks
```
