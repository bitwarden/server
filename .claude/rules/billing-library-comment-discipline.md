---
paths:
  - "src/Libraries/Invoicing/**"
  - "src/Libraries/Subscriptions.Organization/**"
  - "src/Libraries/Subscriptions.User/**"
---

# Library comment discipline

Public API gets `///` doc comments — terse, but they state the contract LIBRARY.md requires:
the guarantees a consumer can rely on and the invariants callers must uphold. Everything else
is near comment-free. A non-doc comment is only for a non-obvious why, and stays one line.
