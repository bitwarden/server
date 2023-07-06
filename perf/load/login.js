import { authenticate } from "./helpers/auth.js";

const IDENTITY_URL = __ENV.IDENTITY_URL;
const CLIENT_ID = __ENV.CLIENT_ID;
const AUTH_USERNAME = __ENV.AUTH_USER_EMAIL;
const AUTH_PASSWORD = __ENV.AUTH_USER_PASSWORD_HASH;

export const options = {
  ext: {
    loadimpact: {
      projectID: 3639465,
      name: "Login",
    },
  },
  scenarios: {
    constant_load: {
      executor: "constant-arrival-rate",
      rate: 2,
      timeUnit: "1s", // 2 requests / second
      duration: "10m",
      preAllocatedVUs: 10,
    },
    ramping_load: {
      executor: "ramping-arrival-rate",
      startRate: 60,
      timeUnit: "1m", // 1 request / second to start
      stages: [
        { duration: "30s", target: 60 },
        { duration: "5m", target: 120 },
        { duration: "2m", target: 150 },
        { duration: "1m", target: 180 },
        { duration: "30s", target: 200 },
        { duration: "30s", target: 90 },
        { duration: "30s", target: 0 },
      ],
      preAllocatedVUs: 25,
    },
  },
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<1500"],
  },
};

export default function (data) {
  authenticate(IDENTITY_URL, CLIENT_ID, AUTH_USERNAME, AUTH_PASSWORD);
}
