#!/usr/bin/env bash

# Get the dev directory (parent of helpers)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEV_DIR="$(dirname "$SCRIPT_DIR")"

cd "$DEV_DIR"

openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout identity_server_dev.key -out identity_server_dev.crt \
    -subj "/CN=Bitwarden Identity Server Dev" -days 3650
openssl pkcs12 -export -legacy -out identity_server_dev.pfx -inkey identity_server_dev.key -in identity_server_dev.crt \
    -certfile identity_server_dev.crt -passout pass:

security import ./identity_server_dev.pfx -k ~/Library/Keychains/login.keychain

identity=($(openssl x509 -in identity_server_dev.crt -outform der | shasum -a 1 | tr a-z A-Z));

echo "Certificate fingerprints:"

echo "Identity Server Dev: ${identity}"
