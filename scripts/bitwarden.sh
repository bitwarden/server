#!/usr/bin/env bash
set -e

cat << "EOF"
 _     _ _                         _            
| |__ (_) |___      ____ _ _ __ __| | ___ _ __  
| '_ \| | __\ \ /\ / / _` | '__/ _` |/ _ \ '_ \ 
| |_) | | |_ \ V  V / (_| | | | (_| |  __/ | | |
|_.__/|_|\__| \_/\_/ \__,_|_|  \__,_|\___|_| |_|
EOF

cat << EOF
Open source password management solutions
Copyright 2015-$(date +'%Y'), 8bit Solutions LLC
https://bitwarden.com, https://github.com/bitwarden
===================================================
EOF

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
SCRIPT_NAME=$(basename "$0")
SCRIPT_PATH="$DIR/$SCRIPT_NAME"
BITWARDEN_SCRIPT_URL="https://go.btwrdn.co/bw-sh"

if curl -L -s -w "http_code %{http_code}" -o $SCRIPT_PATH.1 $BITWARDEN_SCRIPT_URL | grep -q "^http_code 20[0-9]"
then
    mv $SCRIPT_PATH.1 $SCRIPT_PATH
    chmod u+x $SCRIPT_PATH
    echo "We have moved our self-hosted scripts to their own repository (https://github.com/bitwarden/self-host).  Your 'bitwarden.sh' script has been automatically upgraded. Please run it again."
else
    rm -f $SCRIPT_PATH.1
fi
