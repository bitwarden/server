#!/bin/bash

USERNAME="bitwarden"
NOUSER=`id -u $USERNAME > /dev/null 2>&1; echo $?`
LUID=${LOCAL_UID:-999}

# Step down from host root
if [ $LUID == 0 ]
then
    LUID=999
fi

if [ $NOUSER == 0 ] && [ `id -u $USERNAME` != $LUID ]
then
    usermod -u $LUID $USERNAME
elif [ $NOUSER == 1 ]
then
    useradd -r -u $LUID -g $USERNAME $USERNAME
fi

chown -R $USERNAME:$USERNAME /etc/bitwarden
cp /etc/bitwarden/nginx/default.conf /etc/nginx/conf.d/default.conf
mkdir -p /etc/letsencrypt
chown -R $USERNAME:$USERNAME /etc/letsencrypt
mkdir -p /etc/ssl
chown -R $USERNAME:$USERNAME /etc/ssl
touch /var/run/nginx.pid
chown -R $USERNAME:$USERNAME /var/run/nginx.pid
chown -R $USERNAME:$USERNAME /var/cache/nginx
chown -R $USERNAME:$USERNAME /var/log/nginx

gosu $USERNAME:$USERNAME nginx -g 'daemon off;'
