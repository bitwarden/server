#!/bin/sh

# Generate Identity certificate
if [ ! -f /etc/bitwarden/identity.pfx ]; then
  openssl req \
  -x509 \
  -newkey rsa:4096 \
  -sha256 \
  -nodes \
  -keyout /etc/bitwarden/identity.key \
  -out /etc/bitwarden/identity.crt \
  -subj "/CN=Bitwarden IdentityServer" \
  -days 36500
  
  openssl pkcs12 \
  -export \
  -out /etc/bitwarden/identity.pfx \
  -inkey /etc/bitwarden/identity.key \
  -in /etc/bitwarden/identity.crt \
  -passout pass:$globalSettings__identityServer__certificatePassword
  
  rm /etc/bitwarden/identity.crt
  rm /etc/bitwarden/identity.key
fi

cp /etc/bitwarden/identity.pfx /app/Identity/identity.pfx
cp /etc/bitwarden/identity.pfx /app/Sso/identity.pfx

# Generate SSL certificates
if [ "$BW_ENABLE_SSL" == "true" -a ! -f /etc/bitwarden/ssl.key ]; then
  openssl req \
  -x509 \
  -newkey rsa:4096 \
  -sha256 \
  -nodes \
  -days 36500 \
  -keyout /etc/bitwarden/${BW_SSL_KEY:-ssl.key} \
  -out /etc/bitwarden/${BW_SSL_CERT:-ssl.crt} \
  -reqexts SAN \
  -extensions SAN \
  -config <(cat /etc/ssl/openssl.cnf <(printf "[SAN]\nsubjectAltName=DNS:${DOMAIN:-localhost}\nbasicConstraints=CA:true")) \
  -subj "/C=US/ST=California/L=Santa Barbara/O=Bitwarden Inc./OU=Bitwarden/CN=${DOMAIN:-localhost}"
fi

# Launch a loop to rotate nginx logs on a daily basis
/bin/sh -c "/logrotate.sh loop >/dev/null 2>&1 &"

/usr/local/bin/confd -onetime -backend env

exec /usr/bin/supervisord
