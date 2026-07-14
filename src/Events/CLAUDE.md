# Events Pipeline - App Context

> Scope: `src/Events/`. Supplements the repo-wide `/.claude/CLAUDE.md`. The DIRT team file at
> `/src/Core/Dirt/CLAUDE.md` does NOT auto-load here (different subtree), so the key team context
> is linked below.

## What this app does

`src/Events/` is the Events collector: a lightweight ASP.NET app that ingests organization/user
audit events (the `/collect` path via `EventsController`) and hands them to the event write pipeline.
Its sibling `src/EventsProcessor/` (`AzureQueueHostedService`) drains the Azure queue and persists
events (Azure Table Storage in cloud; database self-hosted). When Event Integrations are enabled, the
`EventIntegrationEventWriteService` also broadcasts events onto the AMQP exchange (see
`/src/Core/Dirt/EventIntegrations/README.md`).

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

- Event write / integration pipeline: `/src/Core/Dirt/EventIntegrations/README.md`
- DIRT team context: `/src/Core/Dirt/CLAUDE.md`
- Repo-wide rules: `/.claude/CLAUDE.md`
