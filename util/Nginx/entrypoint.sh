#!/bin/bash

# Setup

GROUPNAME="bitwarden"
USERNAME="bitwarden"

CURRENTGID=`getent group $GROUPNAME | cut -d: -f3`
LGID=${LOCAL_GID:-999}

NOUSER=`id -u $USERNAME > /dev/null 2>&1; echo $?`
LUID=${LOCAL_UID:-999}

# Step down from host root

if [ $LGID == 0 ]
then
    LGID=999
fi

if [ $LUID == 0 ]
then
    LUID=999
fi

# Create group

if [ $CURRENTGID ]
then
    if [ "$CURRENTGID" != "$LGID" ]
    then
        groupmod -g $LGID $GROUPNAME
    fi
else
    groupadd -g $LGID $GROUPNAME
fi

# Create user and assign group

if [ $NOUSER == 0 ] && [ `id -u $USERNAME` != $LUID -o `id -g $USERNAME` != $LGID ]
then
    usermod -u $LUID -g $GROUPNAME $USERNAME
elif [ $NOUSER == 1 ]
then
    useradd -r -u $LUID -g $GROUPNAME $USERNAME
fi

# Make home directory for user

if [ ! -d "/home/$USERNAME" ]
then
    mkhomedir_helper $USERNAME
fi

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

gosu $USERNAME:$GROUPNAME nginx -g 'daemon off;'
