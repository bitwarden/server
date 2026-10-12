#!/usr/bin/env python3
"""Set up and trigger a PAM credential-rotation job from the admin side, on the local dev stack.

Drives the admin HTTP surface as the admin console would, on seeded data in vault_dev, and
leaves a claimable job for a real access connector to execute and report on:

  ADMIN (owner bearer, client_credentials on the seeded user's ApiKey)
    1. register a target system (phase 1)      POST  organizations/{org}/access-connectors/rotation/target-systems
       seal it (phase 2)                        PUT   organizations/{org}/access-connectors/rotation/target-systems/{id}
       (or reuse one via --target-id)
    2. assign the connector to that target     POST  organizations/{org}/access-connectors/{id}/assignments
    3. create a rotation config for a cipher   POST  organizations/{org}/access-connectors/rotation/configs
    4. trigger an on-demand rotation           POST  organizations/{org}/access-connectors/rotation/configs/{id}/rotate

The server never parses `sealedData` — it is an opaque blob the admin client seals under the org
key. This script has no org key, so it sends a placeholder string instead of a real seal. That is
enough to exercise the admin/dispatch side and leave a claimable job, but a real access connector
will refuse the job at claim-verification time (it cannot unseal a fake blob). For an end-to-end
run against a real connector, seal the target and config from the admin client instead of this
script.

By default it creates a fresh target and config on a fresh cipher, so repeated runs avoid the
on-demand cooldown and the active-job guard.

Usage:
  python3 dev/pam-rotation-sim.py \
      --org-id 34C5C52C-AC9A-4D53-878B-B46600CA936C \
      --admin-email enterprise.owner@redwood.example \
      --connector-id <access connector id> [--target-id <guid>] [--cipher-id <guid>] [--cleanup]
"""
import argparse
import json
import os
import re
import subprocess
import urllib.error
import urllib.parse
import urllib.request

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SECRETS_JSON = os.path.join(REPO, "dev/secrets.json")


def sql_password():
    raw = open(SECRETS_JSON).read()
    for conn in re.findall(r'"connectionString"\s*:\s*"([^"]+)"', raw):
        if "Database=vault_dev;" in conn:
            return re.search(r"Password=([^;]+);", conn).group(1)
    raise SystemExit("Could not find the vault_dev connection string in dev/secrets.json.")


def query1(container, password, sql):
    """Run a scalar query and return the single trimmed value (or None)."""
    out = subprocess.run(
        ["docker", "exec", container,
         "/opt/mssql-tools18/bin/sqlcmd", "-C", "-S", "localhost", "-U", "SA",
         "-P", password, "-d", "vault_dev", "-h", "-1", "-W", "-Q",
         "SET NOCOUNT ON;\n" + sql],
        capture_output=True, text=True, check=True).stdout
    rows = [l.strip() for l in out.splitlines() if l.strip()]
    return rows[0] if rows else None


def http(method, url, bearer, body=None, allow=()):
    """Issue a request; raise on unexpected error, but return (status, None) for statuses in `allow`."""
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method, headers={
        "Authorization": f"Bearer {bearer}",
        **({"Content-Type": "application/json"} if data is not None else {})})
    try:
        with urllib.request.urlopen(req) as r:
            raw = r.read()
            return r.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        if e.code in allow:
            return e.code, None
        raise SystemExit(f"{method} {url} -> {e.code}\n{e.read().decode(errors='replace')}")


def admin_token(identity_base, client_version, uid, ukey):
    req = urllib.request.Request(
        f"{identity_base}/connect/token",
        data=urllib.parse.urlencode({
            "grant_type": "client_credentials", "scope": "api",
            "client_id": f"user.{uid}", "client_secret": ukey,
            "deviceType": "21", "deviceIdentifier": "pam-rotation-sim",
            "deviceName": "pam-rotation-sim"}).encode(),
        headers={"Content-Type": "application/x-www-form-urlencoded",
                 "Bitwarden-Client-Version": client_version})
    with urllib.request.urlopen(req) as r:
        return json.loads(r.read())["access_token"]


def step(n, msg):
    print(f"\n[{n}] {msg}")


def fake_sealed_data(note):
    """A placeholder in the real sealed-blob shape ({formatVersion, wrappedCek, envelope}); not a real seal — this
    script has no org key. A real access connector will refuse to unseal it."""
    return json.dumps({"formatVersion": 1, "wrappedCek": "fake", "envelope": f"fake-dev-sim:{note}"})


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--org-id", required=True)
    ap.add_argument("--admin-email", required=True)
    ap.add_argument("--connector-id", required=True, help="access connector id to assign to the target")
    ap.add_argument("--target-id", help="reuse an existing target system instead of registering one")
    ap.add_argument("--cipher-id", help="org cipher to rotate; auto-selected if omitted")
    ap.add_argument("--account-identity", default="svc-rotation@redwood.example")
    ap.add_argument("--cleanup", action="store_true",
                    help="delete the created rotation config at the end (cascades the job)")
    ap.add_argument("--container", default="bitwardenserver-mssql-1")
    ap.add_argument("--api-base", default="http://localhost:4000")
    ap.add_argument("--identity-base", default="http://localhost:33656")
    ap.add_argument("--client-version", default="2026.5.0")
    args = ap.parse_args()

    org = args.org_id
    pw = sql_password()
    api = args.api_base.rstrip("/")

    uk = query1(args.container, pw,
                f"SELECT CAST(U.Id AS varchar(64))+'|'+U.ApiKey FROM [User] U WHERE U.Email='{args.admin_email}';")
    if not uk:
        raise SystemExit(f"No user found for {args.admin_email}")
    uid, ukey = uk.split("|")
    admin = admin_token(args.identity_base, args.client_version, uid, ukey)

    cipher_id = args.cipher_id or query1(args.container, pw,
        f"""SELECT TOP 1 CAST(Id AS varchar(64)) FROM Cipher
            WHERE OrganizationId='{org}' AND Type=1
              AND Id NOT IN (SELECT CipherId FROM PamRotationConfig);""")
    if not cipher_id:
        raise SystemExit("No org login cipher available without an existing rotation config.")
    print(f"cipher to rotate = {cipher_id}")

    if args.target_id:
        target_id = args.target_id
        print(f"\n[1] reusing target system {target_id}")
    else:
        step(1, "register target system (phase 1: name + method, status=pending_seal)")
        _, target = http("POST", f"{api}/organizations/{org}/access-connectors/rotation/target-systems", admin, {
            "name": f"sim-{cipher_id[:8]}",
            "method": 0})                # Automatic
        target_id = target["id"]
        print(f"    targetSystemId = {target_id} (pendingSeal)")

        step("1b", "seal target system (phase 2: moves pending_seal -> active)")
        http("PUT", f"{api}/organizations/{org}/access-connectors/rotation/target-systems/{target_id}", admin, {
            "name": f"sim-{cipher_id[:8]}",
            "sealedData": fake_sealed_data(f"target:{target_id}")})
        print("    sealed (204)")

    step(2, "assign access connector to target")
    status, _ = http("POST", f"{api}/organizations/{org}/access-connectors/{args.connector_id}/assignments",
                     admin, {"targetSystemId": target_id}, allow=(409,))
    print("    already assigned (409)" if status == 409 else "    assigned (204)")

    step(3, "create rotation config")
    _, config = http("POST", f"{api}/organizations/{org}/access-connectors/rotation/configs", admin, {
        "cipherId": cipher_id, "targetSystemId": target_id,
        "sealedData": fake_sealed_data(f"config:{cipher_id}:{args.account_identity}"),
        "scheduleCron": None, "rotateOnAccessEnd": False})
    config_id = config["config"]["id"]   # POST returns the detail view: { "config": {...}, "jobs": [...] }
    print(f"    configId = {config_id}")

    step(4, "trigger on-demand rotation (creates a claimable job)")
    http("POST", f"{api}/organizations/{org}/access-connectors/rotation/configs/{config_id}/rotate", admin)
    print("    triggered (204)")

    job = query1(args.container, pw,
                 f"""SELECT TOP 1 CAST(Id AS varchar(64))+' status='+CAST(Status AS varchar)
                     FROM PamRotationJob WHERE RotationConfigId='{config_id}' ORDER BY CreationDate DESC;""")
    print(f"\n=== ready for the access connector ===")
    print(f"pending job: {job}   (PamRotationJobStatus 0=Pending)")
    print(f"the assigned access connector ({args.connector_id}) will now see this job on its next poll of "
          f"access-connectors/rotation/jobs")

    if args.cleanup:
        http("DELETE", f"{api}/organizations/{org}/access-connectors/rotation/configs/{config_id}", admin)
        print(f"\ncleaned up: deleted config {config_id} (job cascaded)")
    else:
        print(f"\nartifacts kept: target={target_id} config={config_id}")


if __name__ == "__main__":
    main()
