import http from "k6/http";
import { check, fail } from "k6";
import { authenticate } from "./helpers/auth.js";

const IDENTITY_URL = __ENV.IDENTITY_URL;
const API_URL = __ENV.API_URL;
const CLIENT_ID = __ENV.CLIENT_ID;
const AUTH_USERNAME = __ENV.AUTH_USER_EMAIL;
const AUTH_PASSWORD = __ENV.AUTH_USER_PASSWORD_HASH;

export const options = {
  ext: {
    loadimpact: {
      projectID: 3639465,
      name: "Config",
    },
  },
  stages: [
    { duration: "30s", target: 10 },
    { duration: "1m", target: 20 },
    { duration: "2m", target: 25 },
    { duration: "30s", target: 0 },
  ],
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<1000"],
  },
};

export function setup() {
  return authenticate(IDENTITY_URL, CLIENT_ID, AUTH_USERNAME, AUTH_PASSWORD);
}

export default function (data) {
  const params = {
    headers: {
      Accept: "application/json",
      Authorization: `Bearer ${data.access_token}`,
      "X-ClientId": CLIENT_ID,
    },
    tags: { name: "Config" },
  };

  const res = http.get(`${API_URL}/config`, params);
  if (
    !check(res, {
      "config status is 200": (r) => r.status === 200,
    })
  ) {
    fail("config status code was *not* 200");
  }

  const json = res.json();

  check(json, {
    "config version is available": (j) => j.version !== "",
  });
}
