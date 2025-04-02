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
      name: "Sync",
    },
  },
  scenarios: {
    constant_load: {
      executor: "constant-arrival-rate",
      rate: 30,
      timeUnit: "1m", // 0.5 requests / second
      duration: "10m",
      preAllocatedVUs: 5,
    },
    ramping_load: {
      executor: "ramping-arrival-rate",
      startRate: 30,
      timeUnit: "1m", // 0.5 requests / second to start
      stages: [
        { duration: "30s", target: 30 },
        { duration: "2m", target: 75 },
        { duration: "1m", target: 60 },
        { duration: "2m", target: 100 },
        { duration: "2m", target: 90 },
        { duration: "1m", target: 120 },
        { duration: "30s", target: 150 },
        { duration: "30s", target: 60 },
        { duration: "30s", target: 0 },
      ],
      preAllocatedVUs: 20,
    },
  },
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<1200"],
  },
};

export function setup() {
  return authenticate(IDENTITY_URL, CLIENT_ID, AUTH_USERNAME, AUTH_PASSWORD);
}

export default function (data) {
  const params = {
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      Authorization: `Bearer ${data.access_token}`,
      "X-ClientId": CLIENT_ID,
    },
    tags: { name: "Sync" },
  };

  const excludeDomains = Math.random() > 0.5;
  
  const syncRes = http.get(`${API_URL}/sync?excludeDomains=${excludeDomains}`, params);
  if (
    !check(syncRes, {
      "sync status is 200": (r) => r.status === 200,
    })
  ) {
    console.error(`Sync failed with status ${syncRes.status}: ${syncRes.body}`);
    fail("sync status code was *not* 200");
  }

  if (syncRes.status === 200) {
    const syncJson = syncRes.json();

    check(syncJson, {
      "sync response has profile": (j) => j.profile !== undefined,
      "sync response has folders": (j) => Array.isArray(j.folders),
      "sync response has collections": (j) => Array.isArray(j.collections),
      "sync response has ciphers": (j) => Array.isArray(j.ciphers),
      "sync response has policies": (j) => Array.isArray(j.policies),
      "sync response has sends": (j) => Array.isArray(j.sends),
      "sync response has correct object type": (j) => j.object === "sync"
    });
  }
}
