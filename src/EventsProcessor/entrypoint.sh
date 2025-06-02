#!/usr/bin/env bash

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

    groupadd -o -g $LGID $GROUPNAME >/dev/null 2>&1 ||
    groupmod -o -g $LGID $GROUPNAME >/dev/null 2>&1
    useradd -o -u $LUID -g $GROUPNAME -s /bin/false $USERNAME >/dev/null 2>&1 ||
    usermod -o -u $LUID -g $GROUPNAME -s /bin/false $USERNAME >/dev/null 2>&1
    mkhomedir_helper $USERNAME

    # The rest...

    chown -R $USERNAME:$GROUPNAME /app
    mkdir -p /etc/bitwarden/logs
    mkdir -p /etc/bitwarden/ca-certificates
    chown -R $USERNAME:$GROUPNAME /etc/bitwarden

    gosu_cmd="gosu $USERNAME:$GROUPNAME"
else
    gosu_cmd=""
fi

exec $gosu_cmd /app/EventsProcessor
