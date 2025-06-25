#!/bin/sh

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

if [ "$(id -u)" = "0" ]
then
    # Create user and group

    addgroup -g "$LGID" -S "$GROUPNAME" 2>/dev/null || true
    adduser -u "$LUID" -G "$GROUPNAME" -S -D -H "$USERNAME" 2>/dev/null || true
    mkdir -p /home/$USERNAME
    chown $USERNAME:$GROUPNAME /home/$USERNAME

    # The rest...

    chown -R $USERNAME:$GROUPNAME /app
    mkdir -p /bitwarden/env
    mkdir -p /bitwarden/docker
    mkdir -p /bitwarden/ssl
    mkdir -p /bitwarden/letsencrypt
    mkdir -p /bitwarden/identity
    mkdir -p /bitwarden/nginx
    mkdir -p /bitwarden/ca-certificates
    chown -R $USERNAME:$GROUPNAME /bitwarden

    gosu_cmd="gosu $USERNAME:$GROUPNAME"
else
    gosu_cmd=""
fi

exec $gosu_cmd "$@"
