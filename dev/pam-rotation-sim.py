#!/usr/bin/env python3
"""Set up and trigger a PAM credential-rotation job from the admin side, on the local dev stack.

LOCAL DEV ONLY. Drives the admin HTTP surface exactly as the admin console would, then
leaves a claimable job for a *real* rotation daemon to pick up, execute, and report on:

  ADMIN (owner bearer, client_credentials on the seeded user's ApiKey)
    1. register an automatic target system   POST  organizations/{org}/rotation/target-systems
       (or reuse one via --target-id)
    2. assign the daemon to that target        POST  organizations/{org}/rotation/daemons/{id}/assignments
    3. create a rotation config for a cipher    POST  organizations/{org}/rotation/configs
    4. trigger an on-demand rotation            POST  organizations/{org}/rotation/configs/{id}/rotate

The daemon side (poll rotation/daemon/jobs -> claim -> read/write cipher -> report) is NOT
done here -- an actual daemon handles that. By default this creates a fresh target + config
on a fresh cipher, which sidesteps the on-demand cooldown and the "config already has an
active job" guard so repeated runs don't 400.

This is all seeded synthetic data in vault_dev.

Usage:
  python3 dev/pam-rotation-sim.py \
      --org-id 34C5C52C-AC9A-4D53-878B-B46600CA936C \
      --admin-email enterprise.owner@redwood.example \
      --daemon-id <PamDaemon.Id> [--target-id <guid>] [--cipher-id <guid>] [--cleanup]
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


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--org-id", required=True)
    ap.add_argument("--admin-email", required=True)
    ap.add_argument("--daemon-id", required=True, help="PamDaemon.Id to assign to the target")
    ap.add_argument("--kind", default="entra", choices=["entra", "mssql", "customscript"],
                    help="automatic connector kind for a newly registered target (default: entra)")
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

    # --- admin bearer (seeded user's ApiKey via client_credentials) ---
    uk = query1(args.container, pw,
                f"SELECT CAST(U.Id AS varchar(64))+'|'+U.ApiKey FROM [User] U WHERE U.Email='{args.admin_email}';")
    if not uk:
        raise SystemExit(f"No user found for {args.admin_email}")
    uid, ukey = uk.split("|")
    admin = admin_token(args.identity_base, args.client_version, uid, ukey)

    # --- pick a cipher with no existing rotation config ---
    cipher_id = args.cipher_id or query1(args.container, pw,
        f"""SELECT TOP 1 CAST(Id AS varchar(64)) FROM Cipher
            WHERE OrganizationId='{org}' AND Type=1
              AND Id NOT IN (SELECT CipherId FROM PamRotationConfig);""")
    if not cipher_id:
        raise SystemExit("No org login cipher available without an existing rotation config.")
    print(f"cipher to rotate = {cipher_id}")

    # === ADMIN SETUP ===
    if args.target_id:
        target_id = args.target_id
        print(f"\n[1] reusing target system {target_id}")
    else:
        kind_val = {"entra": 0, "mssql": 1, "customscript": 2}[args.kind]
        step(1, f"register automatic target system (kind={args.kind})")
        _, target = http("POST", f"{api}/organizations/{org}/rotation/target-systems", admin, {
            "name": f"sim-{args.kind}-{cipher_id[:8]}",
            "method": 0,                # Automatic
            "kind": kind_val,
            "passwordPolicy": {"minLength": 16, "maxLength": 32,
                               "includeUppercase": True, "includeLowercase": True,
                               "includeDigits": True, "includeSymbols": True},
            "supportsSessionTermination": False})
        target_id = target["id"]
        print(f"    targetSystemId = {target_id}")

    step(2, "assign daemon to target")
    status, _ = http("POST", f"{api}/organizations/{org}/rotation/daemons/{args.daemon_id}/assignments",
                     admin, {"targetSystemId": target_id}, allow=(409,))
    print("    already assigned (409)" if status == 409 else "    assigned (204)")

    step(3, "create rotation config")
    _, config = http("POST", f"{api}/organizations/{org}/rotation/configs", admin, {
        "cipherId": cipher_id, "targetSystemId": target_id,
        "accountIdentity": args.account_identity, "terminateSessions": False,
        "scheduleCron": None, "rotateOnAccessEnd": False})
    config_id = config["config"]["id"]   # POST returns the detail view: { "config": {...}, "jobs": [...] }
    print(f"    configId = {config_id}")

    step(4, "trigger on-demand rotation (creates a claimable job)")
    http("POST", f"{api}/organizations/{org}/rotation/configs/{config_id}/rotate", admin)
    print("    triggered (204)")

    # --- show the pending job the real daemon will claim ---
    job = query1(args.container, pw,
                 f"""SELECT TOP 1 CAST(Id AS varchar(64))+' status='+CAST(Status AS varchar)
                     FROM PamRotationJob WHERE RotationConfigId='{config_id}' ORDER BY CreationDate DESC;""")
    print(f"\n=== ready for the daemon ===")
    print(f"pending job: {job}   (PamRotationJobStatus 0=Pending)")
    print(f"the assigned daemon ({args.daemon_id}) will now see this job on its next poll of rotation/daemon/jobs")

    if args.cleanup:
        http("DELETE", f"{api}/organizations/{org}/rotation/configs/{config_id}", admin)
        print(f"\ncleaned up: deleted config {config_id} (job cascaded)")
    else:
        print(f"\nartifacts kept: target={target_id} config={config_id}")


if __name__ == "__main__":
    main()
