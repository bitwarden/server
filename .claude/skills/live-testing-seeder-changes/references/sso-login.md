# SSO login

How to log in through the local IdP. These items are the compose path. If the stack runs under Aspire, stop and ask the developer how to proceed.

1. Seed `features.local-sso`. The stderr output ends with an "SSO wiring" block: the login identifier, the two `IDP_SP_*` lines for `dev/.env`, and the `authsources.php` entry for the owner. Keep a copy of the block; the items below need it.
2. Show the developer the `.env` and `authsources.php` edits from that block, with a username and password you choose in place of `<username>:<password>`, and wait for them to say the edit is in place. The entry's `email` must match the seeded owner exactly, mangle prefix included, and its `uid` must be unique. `saml20-sp-remote.php` is a copy of its `.example`; if a failed `up` left a directory with that name, `rmdir` it first.
3. Restart and check the IdP. Compose `version` and platform warnings are harmless, as is "Headers already sent" in the IdP log on metadata requests.

   ```bash
   (cd dev && docker compose --profile idp up -d)
   curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8090/simplesaml/saml2/idp/metadata.php   # IdP up: 200
   curl -s -o /dev/null -w '%{http_code}\n' http://localhost:51822/saml2/<orgId>                      # SP resolves the org: 200
   ```

4. In the vault: type the owner email, click `[data-testid="login-sso-button"]`, enter the identifier on `#/sso`, fill `#username` and `#password` on the IdP page with the values from item 2, then unlock on `#/lock` with `[data-testid="lock-master-password-input"]`.
5. Verify through SQL: `dbo.SsoUser` has a row for the owner's `UserId` with `ExternalId` equal to the `uid` from `authsources.php`.
