#!/bin/bash

## Create user and group
#
#groupadd -o -g $LGID $GROUPNAME >/dev/null 2>&1 ||
#groupmod -o -g $LGID $GROUPNAME >/dev/null 2>&1
#useradd -o -u $LUID -g $GROUPNAME -s /bin/false $USERNAME >/dev/null 2>&1 ||
#usermod -o -u $LUID -g $GROUPNAME -s /bin/false $USERNAME >/dev/null 2>&1
#mkhomedir_helper $USERNAME
#
## The rest...
#
#chown -R $USERNAME:$GROUPNAME /etc/bitwarden
#cp /etc/bitwarden/nginx/*.conf /etc/nginx/conf.d/
#mkdir -p /etc/letsencrypt
#chown -R $USERNAME:$GROUPNAME /etc/letsencrypt
#mkdir -p /etc/ssl
#chown -R $USERNAME:$GROUPNAME /etc/ssl
#mkdir -p /var/run/nginx
#touch /var/run/nginx/nginx.pid
#chown -R $USERNAME:$GROUPNAME /var/run/nginx
#chown -R $USERNAME:$GROUPNAME /var/cache/nginx
#chown -R $USERNAME:$GROUPNAME /var/log/nginx


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
    -config <(cat /usr/lib/ssl/openssl.cnf <(printf '[SAN]\nsubjectAltName=DNS:${BW_DOMAIN:-localhost}\nbasicConstraints=CA:true')) \
    -subj \"/C=US/ST=California/L=Santa Barbara/O=Bitwarden Inc./OU=Bitwarden/CN=$BW_DOMAIN\"
  fi
fi

# Launch a loop to rotate nginx logs on a daily basis
gosu nginx:nginx /bin/sh -c "/logrotate.sh loop >/dev/null 2>&1 &"

exec gosu nginx:nginx nginx -g 'daemon off;'
