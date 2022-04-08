#!/bin/bash
#
# Scripts in this directory are run during the build process.
# each script will be uploaded to /tmp on your build droplet, 
# given execute permissions and run.  The cleanup process will
# remove the scripts from your build system after they have run
# if you use the build_image task.
#

#
# Make MOTD and boot script executable
#

chmod +x /var/lib/cloud/scripts/per-instance/001_onboot

chmod +x /etc/update-motd.d/99-bitwarden-welcome

#
# Setup First Run Script
# ref: https://github.com/digitalocean/marketplace-partners#running-commands-on-first-login
#

chmod +x /opt/bitwarden/install-bitwarden.sh

echo '/opt/bitwarden/install-bitwarden.sh' >> /root/.bashrc
