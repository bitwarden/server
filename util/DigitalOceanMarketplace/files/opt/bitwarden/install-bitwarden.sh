#!/bin/bash

#
# Install Bitwarden
# ref: https://help.bitwarden.com/article/install-on-premise/
#

echo -e ''
echo -e 'Installing Bitwarden...'
echo -e ''

cd /root
docker-compose up -d

echo -e ''
echo -e 'Bitwarden installation complete.'
echo -e ''

#
# Setup Bitwarden update cron
# ref: https://help.bitwarden.com/article/updating-on-premise/
#

echo -e '#!/usr/bin/env bash\ncd /root\ndocker-composes pull\ndocker-compose up -d' \
    > /etc/cron.weekly/bitwardenupdate

chmod +x /etc/cron.weekly/bitwardenupdate

#
# Cleanup .bashrc
#

cp -f /etc/skel/.bashrc /root/.bashrc
