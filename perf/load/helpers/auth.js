import http from "k6/http";
import { check, fail } from "k6";

// Identity rejects password grants that omit the "Bitwarden-Client-Version"
// header (see ClientVersionValidator), so every request must supply one.
const CLIENT_VERSION = __ENV.CLIENT_VERSION || "2026.8.0";

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
    // Only logged on failure: an unsuccessful /connect/token response carries an
    // OAuth error and message, never a token or other credential material.
    console.error(
      `login failed with status ${res.status}: ${String(res.body).slice(0, 500)}`
    );
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
