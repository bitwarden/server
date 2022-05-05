#!/bin/sh

if [ ! -f /etc/bitwarden/identity/identity.pfx ]; then
  openssl req \
  -x509 \
  -newkey rsa:4096 \
  -sha256 \
  -nodes \
  -keyout identity.key \
  -out identity.crt \
  -subj "/CN=Bitwarden IdentityServer" \
  -days 36500
  
  openssl pkcs12 \
  -export \
  -out /etc/bitwarden/identity/identity.pfx \
  -inkey identity.key \
  -in identity.crt \
  -passout pass:$globalSettings__identityServer__certificatePassword
  
  rm identity.crt
  rm identity.key
fi

cp /etc/bitwarden/identity/identity.pfx /app/identity.pfx

exec dotnet /app/Identity.dll
