# Events Pipeline - App Context

> Scope: `src/Events/`. Supplements the repo-wide `.claude/CLAUDE.md`. The team-level DIRT file at
> `src/Core/Dirt/CLAUDE.md` does NOT auto-load here (different subtree), so the key team context
> is linked below.

## What this app does

`src/Events/` is the Events collector: a lightweight ASP.NET app that ingests organization/user
audit events (the `/collect` path via `EventsController`) and hands them to the event write pipeline.
The write path is selected in `AddEventWriteServices` by deployment:
- **Cloud:** the collector enqueues to Azure Queue Storage, and its sibling `src/EventsProcessor/`
  (`AzureQueueHostedService`) drains the queue and persists events to Azure Table Storage.
- **Self-hosted:** there is no queue and no processor - the collector writes events straight to the
  database via `RepositoryEventWriteService` (selected when `GlobalSettings.SelfHosted` is true).
  `src/EventsProcessor/` is a cloud-only component (it has no `appsettings.SelfHosted.json`).

When Event Integrations are enabled, `EventIntegrationEventWriteService` also broadcasts events onto
the AMQP exchange (see `src/Core/Dirt/EventIntegrations/README.md`).

## Critical rules

- **Events are metadata, not vault data**, but they carry org/user/entity IDs. Never log PII, tokens,
  or anything that could deanonymize a user; keep the zero-knowledge invariant.
- **This is a hot path.** The collector runs at high volume - avoid per-request DB calls where a cached
  lookup exists, and keep handlers cheap. Prefer the existing write-service abstractions over ad-hoc
  persistence.
- **Auth/membership:** logging org events requires validated org membership (see recent `/collect`
  membership guard). Do not weaken those checks.
- Add/maintain xUnit tests in `test/Events.Test`, `test/Events.IntegrationTest`, and
  `test/EventsProcessor.Test`.

## Common commands

- Run locally: `dotnet run --project src/Events`
- Test: `dotnet test test/Events.Test` (and the integration/processor test projects)

## References

- Event write / integration pipeline: `src/Core/Dirt/EventIntegrations/README.md`
- Team-level context: `src/Core/Dirt/CLAUDE.md`
- Repo-wide rules: `.claude/CLAUDE.md`
