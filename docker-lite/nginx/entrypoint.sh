#!/bin/bash

# Generate SSL certificates
if [ -z "$(ls -A /etc/bitwarden/ssl)" ]; then
  openssl req \
  -x509 \
  -newkey rsa:4096 \
  -sha256 \
  -nodes \
  -days 36500 \
  -keyout /etc/bitwarden/ssl/${BW_SSL_KEY:-private.key} \
  -out /etc/bitwarden/ssl/${BW_SSL_CERT:-certificate.crt} \
  -reqexts SAN \
  -extensions SAN \
  -config <(cat /usr/lib/ssl/openssl.cnf <(printf "[SAN]\nsubjectAltName=DNS:${BW_DOMAIN:-localhost}\nbasicConstraints=CA:true")) \
  -subj "/C=US/ST=California/L=Santa Barbara/O=Bitwarden Inc./OU=Bitwarden/CN=${BW_DOMAIN:-localhost}"
fi

/usr/local/bin/confd -onetime -backend env

# Launch a loop to rotate nginx logs on a daily basis
/bin/sh -c "/logrotate.sh loop >/dev/null 2>&1 &"

exec nginx -g 'daemon off;'
