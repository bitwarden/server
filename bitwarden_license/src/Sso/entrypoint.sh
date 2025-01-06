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

mkdir -p /etc/bitwarden/identity
mkdir -p /etc/bitwarden/core
mkdir -p /etc/bitwarden/logs
mkdir -p /etc/bitwarden/ca-certificates
chown -R $USERNAME:$GROUPNAME /etc/bitwarden

if [[ $globalSettings__selfHosted == "true" ]]; then
  cp /etc/bitwarden/identity/identity.pfx /app/identity.pfx
fi

chown -R $USERNAME:$GROUPNAME /app

if [[ $globalSettings__selfHosted == "true" ]]; then
  cp /etc/bitwarden/ca-certificates/*.crt /usr/local/share/ca-certificates/ >/dev/null 2>&1 \
    && update-ca-certificates
fi

if [[ -f "/etc/bitwarden/kerberos/bitwarden.keytab" && -f "/etc/bitwarden/kerberos/krb5.conf" ]]; then
  chown -R $USERNAME:$GROUPNAME /etc/bitwarden/kerberos
  cp -f /etc/bitwarden/kerberos/krb5.conf /etc/krb5.conf
  gosu $USERNAME:$GROUPNAME kinit $globalSettings__kerberosUser -k -t /etc/bitwarden/kerberos/bitwarden.keytab
fi

exec gosu $USERNAME:$GROUPNAME dotnet /app/Sso.dll
