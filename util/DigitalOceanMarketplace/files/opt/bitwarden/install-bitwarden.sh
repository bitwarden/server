#!/bin/bash

#
# Install Bitwarden
# ref: https://help.bitwarden.com/article/install-on-premise/
#

/root/bitwarden.sh install

/root/bitwarden.sh start

echo -e 'Waiting for Bitwarden database container to come online...'

sleep 60s

/root/bitwarden.sh updatedb

#
# Setup Bitwarden update cron
# ref: https://help.bitwarden.com/article/updating-on-premise/
#

echo -e '#!/usr/bin/env bash\n/root/bitwarden.sh updateself\n/root/bitwarden.sh update' \
    > /etc/cron.weekly/bitwarden-update.sh

chmod +x /etc/cron.weekly/bitwarden-update.sh

#
# Cleanup .bashrc
#

cp -f /etc/skel/.bashrc /root/.bashrc
