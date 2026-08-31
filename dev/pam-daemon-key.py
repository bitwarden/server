#!/usr/bin/env python3
"""Build a PAM rotation-daemon credential with a *valid* organization key for local dev.

LOCAL DEV ONLY. Operates on seeded synthetic data in `vault_dev`:
  * Seeded users draw their RSA keypair from the fixed pool in
    util/RustSdk/rust/src/rsa_keys.rs (selected by poolIndex). The private key is
    therefore a known constant in the repo, so we can RSA-OAEP-SHA1 decrypt the
    org key stored (RSA-wrapped) in OrganizationUser.Key -- exactly what a real
    client does at daemon registration.
  * We mirror the Secrets Manager access-token layout (CONTRACT C1): a random
    16-byte seed is generated; the 64-byte symmetric key is *derived* from that seed
    via `bitwarden_crypto::derive_shareable_key(seed, "accesstoken",
    Some("sm-access-token"))`; the payload is encrypted under that derived key; and
    the daemon token embeds the base64-encoded seed (not the key) after the ':'.

This deliberately reconstructs an org key from test data. It is NOT a break of the
zero-knowledge design: the RSA keys are test-only constants committed to the repo,
the master password is the public seeder default, and there is no real vault data.

Usage:
  python3 dev/pam-daemon-key.py \
      --email enterprise.owner@redwood.example \
      --org-id 34C5C52C-AC9A-4D53-878B-B46600CA936C \
      --name local-dev-daemon [--register]
"""
import argparse
import base64
import hashlib
import hmac
import json
import os
import re
import subprocess
import sys
import urllib.request

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
RSA_KEYS_RS = os.path.join(REPO, "util/RustSdk/rust/src/rsa_keys.rs")
SECRETS_JSON = os.path.join(REPO, "dev/secrets.json")

from cryptography.hazmat.primitives.asymmetric import padding
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.serialization import (
    load_pem_private_key, Encoding, PublicFormat,
)
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives.kdf.hkdf import HKDFExpand


def sql_password():
    """Read the SqlServer SA password from dev/secrets.json.

    Parsing the whole JSONC file is brittle (comments, `http://` in strings), so
    pull the first `Database=vault_dev` connection string's Password directly.
    """
    raw = open(SECRETS_JSON).read()
    for conn in re.findall(r'"connectionString"\s*:\s*"([^"]+)"', raw):
        if "Database=vault_dev;" in conn:
            return re.search(r"Password=([^;]+);", conn).group(1)
    raise SystemExit("Could not find the vault_dev connection string in dev/secrets.json.")


def query(container, password, sql):
    out = subprocess.run(
        ["docker", "exec", container,
         "/opt/mssql-tools18/bin/sqlcmd", "-C", "-S", "localhost", "-U", "SA",
         "-P", password, "-d", "vault_dev", "-h", "-1", "-y", "1000", "-Q",
         "SET NOCOUNT ON;\n" + sql],
        capture_output=True, text=True, check=True).stdout
    return [l.rstrip() for l in out.splitlines() if l.strip()]


def pool_keys():
    """Parse every PEM literal from the seeder RSA pool, in declaration order."""
    src = open(RSA_KEYS_RS).read()
    return re.findall(r'TEST_FAKE_RSA_KEY_\d+: &str = "(.*?)";', src, re.S)


def find_private_key(user_pub_b64):
    """Return (poolIndex, loaded_private_key) whose SPKI matches the user's public key."""
    for idx, pem in enumerate(pool_keys()):
        priv = load_pem_private_key((pem + "\n").encode(), password=None)
        spki = base64.b64encode(
            priv.public_key().public_bytes(Encoding.DER, PublicFormat.SubjectPublicKeyInfo)
        ).decode()
        if spki == user_pub_b64:
            return idx, priv
    raise SystemExit("No pool key matched the user's public key (was this user seeded?).")


def encstring_type2(plaintext: bytes, key64: bytes) -> str:
    """Bitwarden EncString type 2: AES-256-CBC + HMAC-SHA256. key64 = 32B enc || 32B mac."""
    enc_key, mac_key = key64[:32], key64[32:]
    iv = os.urandom(16)
    pad = 16 - (len(plaintext) % 16)
    padded = plaintext + bytes([pad]) * pad
    enc = Cipher(algorithms.AES(enc_key), modes.CBC(iv)).encryptor()
    ct = enc.update(padded) + enc.finalize()
    mac = hmac.new(mac_key, iv + ct, hashlib.sha256).digest()
    return f"2.{base64.b64encode(iv).decode()}|{base64.b64encode(ct).decode()}|{base64.b64encode(mac).decode()}"


def derive_daemon_key(seed16: bytes) -> bytes:
    """Derive a 64-byte symmetric key from a 16-byte seed.

    Mirrors `bitwarden_crypto::derive_shareable_key(seed, "accesstoken",
    Some("sm-access-token"))` — CONTRACT C1.

    Step 1: HMAC-SHA256 extract
        prk = HMAC-SHA256(key=b"bitwarden-accesstoken", msg=seed16)
    Step 2: HKDF-Expand (SHA-256, no extract step)
        key64 = HKDFExpand(prk, info=b"sm-access-token", length=64)

    The returned 64 bytes are split enc_key=[:32] / mac_key=[32:] by encstring_type2,
    matching the Aes256CbcHmacKey layout used by the daemon.
    """
    prk = hmac.new(b"bitwarden-accesstoken", seed16, hashlib.sha256).digest()
    hkdf = HKDFExpand(algorithm=hashes.SHA256(), length=64, info=b"sm-access-token")
    return hkdf.derive(prk)


def main():
    # Known-answer self-check (CONTRACT C1 test vector, from token.rs).
    # seed = base64-decode("X8vbvA0bduihIDe/qrzIQQ==")
    # derived key must equal "H9/oIRLtL9nGCQOVDjSMoEbJsjWXSOCb3qeyDt6ckzS3FhyboEDWyTP/CQfbIszNmAVg2ExFganG1FVFGXO/Jg=="
    _kac_seed = base64.b64decode("X8vbvA0bduihIDe/qrzIQQ==")
    _kac_expected = "H9/oIRLtL9nGCQOVDjSMoEbJsjWXSOCb3qeyDt6ckzS3FhyboEDWyTP/CQfbIszNmAVg2ExFganG1FVFGXO/Jg=="
    _kac_actual = base64.b64encode(derive_daemon_key(_kac_seed)).decode()
    if _kac_actual != _kac_expected:
        raise SystemExit(
            f"FATAL: derive_daemon_key known-answer check failed!\n"
            f"  expected: {_kac_expected}\n"
            f"  got:      {_kac_actual}\n"
            "The key-derivation implementation does not match the daemon's CONTRACT C1."
        )

    ap = argparse.ArgumentParser()
    ap.add_argument("--email", required=True)
    ap.add_argument("--org-id", required=True)
    ap.add_argument("--name", default="local-dev-daemon")
    ap.add_argument("--container", default="bitwardenserver-mssql-1")
    ap.add_argument("--register", action="store_true",
                    help="POST the daemon registration to the local API and assemble the token")
    ap.add_argument("--api-base", default="http://localhost:4000")
    ap.add_argument("--identity-base", default="http://localhost:33656")
    ap.add_argument("--client-version", default="2026.5.0")
    args = ap.parse_args()

    pw = sql_password()

    rows = query(args.container, pw, f"""
        SELECT 'PUB='+U.PublicKey FROM [User] U WHERE U.Email='{args.email}';
        SELECT 'OUK='+OU.[Key] FROM OrganizationUser OU
          JOIN [User] U ON U.Id=OU.UserId
          WHERE U.Email='{args.email}' AND OU.OrganizationId='{args.org_id}';""")
    vals = dict(r.split("=", 1) for r in rows if "=" in r)
    user_pub, org_user_key = vals["PUB"].strip(), vals["OUK"].strip()

    idx, priv = find_private_key(user_pub)
    print(f"matched seeder RSA pool index: {idx}")

    assert org_user_key.startswith("4."), f"expected RSA (type 4) org key, got {org_user_key[:2]}"
    org_key = priv.decrypt(
        base64.b64decode(org_user_key[2:]),
        padding.OAEP(mgf=padding.MGF1(hashes.SHA1()), algorithm=hashes.SHA1(), label=None))
    assert len(org_key) == 64, f"unexpected org key length {len(org_key)}"
    org_key_b64 = base64.b64encode(org_key).decode()

    # Mirror the SM access-token layout (CONTRACT C1): generate a 16-byte random
    # seed; derive the 64-byte symmetric key from it; encrypt the payload under
    # that derived key; and store the base64-encoded seed (not the key itself) in
    # the Key field and after the ':' in the final token.
    seed = os.urandom(16)
    seed_b64 = base64.b64encode(seed).decode()
    k = derive_daemon_key(seed)
    payload = json.dumps({"encryptionKey": org_key_b64}).encode()
    encrypted_payload = encstring_type2(payload, k)
    key_field = encstring_type2(seed_b64.encode(), org_key)

    body = {"name": args.name, "encryptedPayload": encrypted_payload, "key": key_field}

    print("\n=== org key (base64, 64 bytes) ===")
    print(org_key_b64)
    print("\n=== registration request body ===")
    print(json.dumps(body, indent=2))

    if not args.register:
        print("\n(dry run -- pass --register to POST and assemble the daemon token)")
        return

    # Owner user's ApiKey drives a client_credentials login to satisfy the admin policy.
    ukrows = query(args.container, pw,
                   f"SELECT 'UK='+CAST(U.Id AS varchar(64))+'|'+U.ApiKey FROM [User] U WHERE U.Email='{args.email}';")
    uid, ukey = dict(r.split("=", 1) for r in ukrows if "=" in r)["UK"].strip().split("|")

    tok = urllib.request.urlopen(urllib.request.Request(
        f"{args.identity_base}/connect/token",
        data=urllib.parse.urlencode({
            "grant_type": "client_credentials", "scope": "api",
            "client_id": f"user.{uid}", "client_secret": ukey,
            "deviceType": "21", "deviceIdentifier": "pam-daemon-key-script",
            "deviceName": "pam-daemon-key-script"}).encode(),
        headers={"Content-Type": "application/x-www-form-urlencoded",
                 "Bitwarden-Client-Version": args.client_version})).read()
    access_token = json.loads(tok)["access_token"]

    reg = urllib.request.urlopen(urllib.request.Request(
        f"{args.api_base}/organizations/{args.org_id}/rotation/daemons",
        data=json.dumps(body).encode(),
        headers={"Content-Type": "application/json",
                 "Authorization": f"Bearer {access_token}"})).read()
    result = json.loads(reg)
    api_key_id = result["apiKeyId"]

    print("\n=== registered daemon ===")
    print(json.dumps(result, indent=2))
    print("\n=== daemon access token (client_id : client_secret : encryption_key) ===")
    print(f"client_id     = daemon.{api_key_id}")
    print(f"client_secret = {result['clientSecret']}")
    print(f"full token    = 0.daemon.{api_key_id}.{result['clientSecret']}:{seed_b64}")


if __name__ == "__main__":
    import urllib.parse  # noqa: E402  (kept local so --dry-run has no import cost surprises)
    main()
