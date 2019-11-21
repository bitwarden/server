#!/bin/bash

# Setup

GROUPNAME="bitwarden"
USERNAME="bitwarden"

LUID=${LOCAL_UID:-0}
LGID=${LOCAL_GID:-0}

# Step down from host root to well-known nobody/nogroup user

if [ $LUID -eq 0 ]
then
    LUID=65534
fi
if [ $LGID -eq 0 ]
then
    LGID=65534
fi

# Create user and group

groupadd -o -g $LGID $GROUPNAME >/dev/null 2>&1 ||
groupmod -o -g $LGID $GROUPNAME >/dev/null 2>&1
useradd -o -u $LUID -g $GROUPNAME -s /bin/false $USERNAME >/dev/null 2>&1 ||
usermod -o -u $LUID -g $GROUPNAME -s /bin/false $USERNAME >/dev/null 2>&1
mkhomedir_helper $USERNAME

# The rest...

chown -R $USERNAME:$GROUPNAME /etc/bitwarden
cp /etc/bitwarden/nginx/default.conf /etc/nginx/conf.d/default.conf
mkdir -p /etc/letsencrypt
chown -R $USERNAME:$GROUPNAME /etc/letsencrypt
mkdir -p /etc/ssl
chown -R $USERNAME:$GROUPNAME /etc/ssl
mkdir -p /var/run/nginx
touch /var/run/nginx/nginx.pid
chown -R $USERNAME:$GROUPNAME /var/run/nginx
chown -R $USERNAME:$GROUPNAME /var/cache/nginx
chown -R $USERNAME:$GROUPNAME /var/log/nginx

# Launch a loop to rotate nginx logs on a daily basis
# User can disable this setting env NGINX_LOGROTATE=0

while [[ "$NGINX_LOGROTATE" != "0" ]]
do
  sleep $((24 * 3600 - (`date +%H` * 3600 + `date +%M` * 60 + `date +%S`)))
  ts=$(date +%Y%m%d_%H%M%S)
  mv /var/log/nginx/access.log /var/log/nginx/access.$ts.log
  mv /var/log/nginx/error.log /var/log/nginx/error.$ts.log
  kill -USR1 `cat /var/run/nginx/nginx.pid`
  sleep 1
  gzip /var/log/nginx/access.$ts.log
  gzip /var/log/nginx/error.$ts.log
  find /var/log/nginx/ -name "*.gz" -mtime +32 -delete
done &
disown %1

exec gosu $USERNAME:$GROUPNAME nginx -g 'daemon off;'
