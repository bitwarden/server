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
touch /var/run/nginx.pid
chown -R $USERNAME:$GROUPNAME /var/run/nginx.pid
chown -R $USERNAME:$GROUPNAME /var/cache/nginx
chown -R $USERNAME:$GROUPNAME /var/log/nginx

exec gosu $USERNAME:$GROUPNAME nginx -g 'daemon off;'
