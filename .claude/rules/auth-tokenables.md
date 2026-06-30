---
paths:
  - "src/Core/Auth/Models/Business/Tokenables/**"
---

# Auth tokenables: mint through a factory

New tokenables in this directory MUST be minted through a dedicated factory — never constructed with `new` at the
call site. A token's expiry and validation are security-relevant: routing every mint through one factory guarantees the
deployment-configured lifetime and the shared validator are always applied, and prevents a caller from issuing a token
with an ad-hoc or missing expiry. Direct `new` construction is the historical pattern here and MUST NOT be used for new tokenables.

## Requirements for a new tokenable

- **The factory is the only mint path.** Add an `I{Name}TokenableFactory` and implementation, registered in DI; it
  MUST be the only supported way to create the token.
- **Lifetime comes from configuration.** The factory MUST set `ExpirationDate` from a `GlobalSettings` value rather than
  a constant hardcoded at the call site, so the token's lifetime is configurable without a redeploy.
- **The lifetime-setting constructor MUST be `internal`.** Leave only the public `[JsonConstructor]` parameterless ctor
  (for deserialization) public, so callers in other assemblies (Identity, Api, Admin) cannot bypass the factory.
- **Expose a static validator.** Provide a static validation method returning a typed `TokenableValidationError?`
  so mint-time and validate-time logic stay consistent.

For broader C#/DI conventions, defer to the `writing-server-code` skill.
