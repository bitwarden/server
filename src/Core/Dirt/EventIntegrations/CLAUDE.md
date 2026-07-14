# Event Integrations - Subsystem Context

> Scope: `src/Core/Dirt/EventIntegrations/`. Supplements the DIRT team file
> `/src/Core/Dirt/CLAUDE.md` and the repo-wide `/.claude/CLAUDE.md`.
>
> **Read first:** `/src/Core/Dirt/EventIntegrations/README.md` - the full design doc
> (two-tier exchange, listener/handler pattern, retries, caching, and a step-by-step
> "Building a new integration" guide). This file only surfaces the rules most easily missed.

## What this subsystem does

Fans out organization audit events to external destinations (Slack, Teams, webhook, HTTP Event
Collector, Datadog) over a two-tier AMQP pipeline that runs on RabbitMQ (self-host) or Azure
Service Bus (cloud). Organizations configure which events go where via `OrganizationIntegration`
+ `OrganizationIntegrationConfiguration`, with optional filters.

## Critical rules & gotchas

- **Two tiers, decoupled listeners/handlers.** Event tier (fan-out of `EventMessage`) ->
  `EventIntegrationHandler` -> integration tier (`IntegrationMessage<T>`) -> per-integration handler.
  Handlers know nothing about the messaging platform; listeners own platform specifics. Keep new
  handlers platform-agnostic and unit-testable in isolation (they return `IntegrationHandlerResult`).
- **Cache invalidation is tag-based and easy to get wrong.** `OrganizationIntegrationConfigurationDetails`
  is served from a named extended cache with a long (1-day) TTL. Admin create/update/delete commands
  MUST invalidate by tag using `EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration`,
  and `EventIntegrationHandler` MUST fetch with the same tag. Change one side without the other and reads
  go stale. See the README "Caching" section.
- **Azure Service Bus subscriptions must exist before deploy.** ASB does NOT create resources on the
  fly (RabbitMQ does). New integrations need their event- and integration-level subscriptions created
  in ASB first, and added to `servicebusemulator_config.json` locally. See README "Deploying a new
  integration."
- **Retries live in the listener, not the handler.** Backoff, jitter, `MaxRetries`, and DLQ routing are
  the listener's job via `IntegrationMessage.ApplyRetry(...)`. Handlers only report success/retryable.

## Adding a new integration

Follow the README's "Building a new integration" checklist exactly (IntegrationType, configuration
models, request/response model switch cases + tests, handler, GlobalSettings queue/subscription names,
`ListenerConfiguration` subclass, `ServiceCollectionExtensions` wiring) and add a row to the README's
integrations table.

## References

- Full design doc: `/src/Core/Dirt/EventIntegrations/README.md`
- Caching internals: `/src/Core/Utilities/CACHING.md`
- DIRT team context: `/src/Core/Dirt/CLAUDE.md`
