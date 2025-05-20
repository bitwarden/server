#!/usr/bin/env bash

PROJECT_NAME="Identity"

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
    mkdir -p /etc/bitwarden/core
    mkdir -p /etc/bitwarden/logs
    mkdir -p /etc/bitwarden/ca-certificates
    chown -R $USERNAME:$GROUPNAME /etc/bitwarden

    if [[ -f "/etc/bitwarden/kerberos/bitwarden.keytab" && -f "/etc/bitwarden/kerberos/krb5.conf" ]]; then
      chown -R $USERNAME:$GROUPNAME /etc/bitwarden/kerberos
    fi

    gosu_cmd="gosu $USERNAME:$GROUPNAME"
else
    gosu_cmd=""
fi

if [[ -f "/etc/bitwarden/kerberos/bitwarden.keytab" && -f "/etc/bitwarden/kerberos/krb5.conf" ]]; then
    cp -f /etc/bitwarden/kerberos/krb5.conf /etc/krb5.conf
    $gosu_cmd kinit $globalSettings__kerberosUser -k -t /etc/bitwarden/kerberos/bitwarden.keytab
fi

if [[ $globalSettings__selfHosted == "true" ]]; then
    if [[ -z $globalSettings__identityServer__certificateLocation ]]; then
        export globalSettings__identityServer__certificateLocation=/etc/bitwarden/identity/identity.pfx
    fi
fi

exec $gosu_cmd /app/"${PROJECT_NAME}"
