#!/bin/bash

# Generate SSL certificates
if [ "$BW_GENERATE_CERT" = "true" ]; then
  if [ -z "$(ls -A /bitwarden/ssl)" ]; then
    openssl req \
    -x509 \
    -newkey rsa:4096 \
    -sha256 \
    -nodes \
    -days 36500 \
    -keyout /bitwarden/ssl/${BW_SSL_KEY:-private.key} \
    -out /bitwarden/ssl/${BW_SSL_CERT:-certificate.crt} \
    -reqexts SAN \
    -extensions SAN \
    -config <(cat /usr/lib/ssl/openssl.cnf <(printf "[SAN]\nsubjectAltName=DNS:${BW_DOMAIN:-localhost}\nbasicConstraints=CA:true")) \
    -subj "/C=US/ST=California/L=Santa Barbara/O=Bitwarden Inc./OU=Bitwarden/CN=${BW_DOMAIN:-localhost}"
  fi
fi

/usr/local/bin/confd -onetime -backend env

# Launch a loop to rotate nginx logs on a daily basis
gosu nginx:nginx /bin/sh -c "/logrotate.sh loop >/dev/null 2>&1 &"

exec gosu nginx:nginx nginx -g 'daemon off;'
