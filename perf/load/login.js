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
  stages: [
    { duration: "30s", target: 10 },
    { duration: "1m", target: 20 },
    { duration: "2m", target: 25 },
    { duration: "30s", target: 0 },
  ],
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<3000"],
  },
};

export default function (data) {
  authenticate(IDENTITY_URL, CLIENT_ID, AUTH_USERNAME, AUTH_PASSWORD);
}
