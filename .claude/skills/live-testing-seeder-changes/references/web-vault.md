# Web vault

How to drive the web vault with the Playwright MCP.

1. Log in by selector: `browser_type` into `[data-testid="login-email-input"]`, then `[data-testid="login-master-password-input"]` with `submit: true`.
2. Switch accounts by clearing `localStorage`, `sessionStorage` and every IndexedDB database, then `location.replace('<vault>/?r=<n>#/login')` with a new `<n>`. A hash-only change keeps the session.
3. Skip onboarding gates by going straight to the route (`#/vault`, `#/organizations/<orgId>/vault`). Click "Skip" when the welcome tour appears; it swallows clicks until dismissed. The tour shows for accounts under 30 days old until that user acknowledges it, and clearing storage resets that, so every seeded account gets it.
4. Lists are virtualized: scroll `main.cdk-virtual-scrollable` in `browser_evaluate` and collect rows. Open an item by `#/vault?collectionId=<c>&itemId=<id>&action=view` and press a real Escape before the next one. Input values are in `input.value`, not `innerText`. An identity's country is the last line of `[data-testid="address"]` (a read-only textarea) and its SSN is `[data-testid="ssn"]`; read both with `.value`.
5. Access Intelligence (`#/organizations/<orgId>/access-intelligence`) needs `UseRiskInsights=1`, which Families plans lack. It sends one HIBP request per login, and large orgs fail on connection resets; that is client behavior, not seeded data.
6. If the Playwright MCP has a persistent profile, every session shares it and Chromium locks it to one browser. Don't drive the browser from two sessions at once.
7. Policy rows (`#/organizations/<orgId>/settings/policies`) are buttons named by title, description and On or Off. A `browser_evaluate` lookup for `bit-dialog` or `[role=dialog]` returned nothing for the policy drawer. Click the row with `browser_click` on a snapshot ref, read the drawer from the snapshot file (`spinbutton`, `checkbox` and `combobox` entries), and close it with `[data-testid="bit-dialog-dismiss"]`.
