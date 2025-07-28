#!/bin/sh

# Setup


if [ -n $1 ]; then
    USERNAME=$1
else
    echo "[!] setup-bwuser.sh is missing username"
    exit 1
fi
if [ -n $2 ]; then
    GROUPNAME=$2
else
    echo "[!] setup-bwuser.sh is missing groupname"
    exit 1
fi

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

addgroup -g "$LGID" -S "$GROUPNAME" 2>/dev/null || true
adduser -u "$LUID" -G "$GROUPNAME" -S -D -H "$USERNAME" 2>/dev/null || true
mkdir -p /home/$USERNAME
chown $USERNAME:$GROUPNAME /home/$USERNAME
