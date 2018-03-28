#!/bin/bash

NOUSER=`id -u bitwarden > /dev/null 2>&1; echo $?`
LUID=${LOCAL_UID:-999}
if [ $NOUSER == 0 ] && [ `id -u bitwarden` != $LUID ]
then
    usermod -u $LUID bitwarden
elif [ $NOUSER == 1 ]
then
    useradd -r -u $LUID -g bitwarden bitwarden
fi

chown -R bitwarden:bitwarden /etc/bitwarden
cp /etc/bitwarden/nginx/default.conf /etc/nginx/conf.d/default.conf
mkdir -p /etc/letsencrypt
chown -R bitwarden:bitwarden /etc/letsencrypt
mkdir -p /etc/ssl
chown -R bitwarden:bitwarden /etc/ssl
touch /var/run/nginx.pid
chown -R bitwarden:bitwarden /var/run/nginx.pid
chown -R bitwarden:bitwarden /var/cache/nginx

gosu bitwarden:bitwarden nginx -g 'daemon off;'
