import http from "k6/http";
import { check, fail } from "k6";
import { authenticate } from "./helpers/auth.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";

const IDENTITY_URL = __ENV.IDENTITY_URL;
const API_URL = __ENV.API_URL;
const CLIENT_ID = __ENV.CLIENT_ID;
const AUTH_CLIENT_ID = __ENV.AUTH_CLIENT_ID;
const AUTH_CLIENT_SECRET = __ENV.AUTH_CLIENT_SECRET;

export const options = {
  ext: {
    loadimpact: {
      projectID: 3639465,
      name: "Groups",
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
    http_req_duration: ["p(95)<1500"],
  },
};

export function setup() {
  return authenticate(
    IDENTITY_URL,
    CLIENT_ID,
    null,
    null,
    AUTH_CLIENT_ID,
    AUTH_CLIENT_SECRET
  );
}

export default function (data) {
  const params = {
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      Authorization: `Bearer ${data.access_token}`,
      "X-ClientId": CLIENT_ID,
    },
    tags: { name: "Groups" },
  };

  let name = `Name ${uuidv4()}`;
  const createPayload = {
    name: name,
    accessAll: true,
    externalId: `External ${uuidv4()}`,
  };

  const createRes = http.post(
    `${API_URL}/public/groups`,
    JSON.stringify(createPayload),
    params
  );
  if (
    !check(createRes, {
      "group create status is 200": (r) => r.status === 200,
    })
  ) {
    fail("group create status code was *not* 200");
  }

  const createJson = createRes.json();

  if (
    !check(createJson, {
      "group create id is available": (j) => j.id !== "",
    })
  ) {
    fail("group create id was *not* available");
  }

  const id = createJson.id;
  const getRes = http.get(`${API_URL}/public/groups/${id}`, params);
  if (
    !check(getRes, {
      "group get status is 200": (r) => r.status === 200,
    })
  ) {
    fail("group get status code was *not* 200");
  }

  const getJson = getRes.json();

  if (
    !check(getJson, {
      "group get name matches": (j) => j.name === name,
    })
  ) {
    fail("group get name did *not* match");
  }

  name = `Name ${uuidv4()}`;
  const updatePayload = {
    name: name,
    accessAll: createPayload.accessAll,
    externalId: createPayload.externalId,
  };

  const updateRes = http.put(
    `${API_URL}/public/groups/${id}`,
    JSON.stringify(updatePayload),
    params
  );
  if (
    !check(updateRes, {
      "group update status is 200": (r) => r.status === 200,
    })
  ) {
    fail("group update status code was *not* 200");
  }

  const deleteRes = http.del(`${API_URL}/public/groups/${id}`, null, params);
  if (
    !check(deleteRes, {
      "group delete status is 200": (r) => r.status === 200,
    })
  ) {
    fail("group delete status code was *not* 200");
  }
}
