import http from "k6/http";
import { check, fail } from "k6";
import exec from "k6/execution";

// Identity rejects password grants that omit the "Bitwarden-Client-Version"
// header (see ClientVersionValidator), so every request must supply one.
const CLIENT_VERSION = __ENV.CLIENT_VERSION;

/**
 * Authenticate using OAuth against Bitwarden
 * @function
 * @param {string} identityUrl - Identity Server URL
 * @param {string} clientHeader - X-ClientId header value
 * @param {string} username - User email (password grant)
 * @param {string} password - User password (password grant)
 * @param {string} clientId - Client ID (client credentials grant)
 * @param {string} clientSecret - Client secret (client credentials grant)
 */
export function authenticate(
  identityUrl,
  clientHeader,
  username,
  password,
  clientId,
  clientSecret
) {
  if (!CLIENT_VERSION) {
    // Aborts the whole test: a throw here only kills the iteration, which would
    // exit 0 on zero samples. Cannot run at init, where the k6 action validates
    // scripts without env.
    exec.test.abort("CLIENT_VERSION env var is required");
  }

  const url = `${identityUrl}/connect/token`;
  const params = {
    headers: {
      Accept: "application/json",
      "X-ClientId": clientHeader,
      "Bitwarden-Client-Version": CLIENT_VERSION,
    },
    tags: { name: "Login" },
  };
  const payload = {
    deviceIdentifier: "a455f262-3d24-4bcd-b178-39dcd67d5c3f",
  };

  if (username !== null) {
    payload["scope"] = "api offline_access";
    payload["grant_type"] = "password";
    payload["client_id"] = "web";
    payload["deviceType"] = "9";
    payload["deviceName"] = "chrome";
    payload["username"] = username;
    payload["password"] = password;
  } else {
    payload["scope"] = "api.organization";
    payload["grant_type"] = "client_credentials";
    payload["client_id"] = clientId;
    payload["client_secret"] = clientSecret;
  }

  const res = http.post(url, payload, params);

  if (
    !check(res, {
      "login status is 200": (r) => r.status === 200,
    })
  ) {
    // A rejected grant can carry the user's email and a 2FA session token, so
    // log only the OAuth error fields rather than the whole body.
    let detail;
    try {
      const body = res.json();
      detail = `${body?.error ?? ""} ${body?.error_description ?? ""}`.trim();
      if (!detail) {
        detail = "<no OAuth error fields in body>";
      }
    } catch {
      detail = "<non-JSON body omitted>";
    }
    console.error(`login failed with status ${res.status}: ${detail}`);
    fail(`login status code was *not* 200, got ${res.status}`);
  }

  const json = res.json();

  if (
    !check(json, {
      "login access token is available": (j) => j.access_token !== "",
    })
  ) {
    fail("login access token was *not* available");
  }

  return json;
}
