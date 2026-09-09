#!/usr/bin/env bash
set -euo pipefail

openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout identity_server_dev.key -out identity_server_dev.crt \
    -subj "/CN=Bitwarden Identity Server Dev" -days 3650

# macOS ships with LibreSSL by default;
# it will often be replaced by users with OpenSSL v3.
# If OpenSSL v3, a -legacy flag is required.
# !NOTE: If on OpenSSL v3, you must use a non-empty password.
if [[ "$(openssl version)" == OpenSSL\ 3* ]]; then
    openssl pkcs12 -export -legacy -out identity_server_dev.pfx -inkey identity_server_dev.key \
        -in identity_server_dev.crt -certfile identity_server_dev.crt
else
    openssl pkcs12 -export -out identity_server_dev.pfx -inkey identity_server_dev.key \
        -in identity_server_dev.crt -certfile identity_server_dev.crt
fi

# Use default system keychain
security import ./identity_server_dev.pfx

identity=($(openssl x509 -in identity_server_dev.crt -outform der | shasum -a 1 | tr a-z A-Z));

echo "Certificate fingerprints:"

echo "Identity Server Dev: ${identity}"
