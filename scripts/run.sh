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
BITWARDEN_SCRIPT_URL="https://go.btwrdn.co/bw-sh"

cd $DIR
cd ../../

FOUND=false

for i in *.sh; do
  if [ $i = "bitwarden.sh" ]
  then
    FOUND=true
    if curl -L -s -w "http_code %{http_code}" -o bitwarden.sh.1 $BITWARDEN_SCRIPT_URL | grep -q "^http_code 20[0-9]"
    then
      mv bitwarden.sh.1 bitwarden.sh
      chmod u+x bitwarden.sh
      echo "We have moved our self-hosted scripts to their own repository (https://github.com/bitwarden/self-host).  Your 'bitwarden.sh' script has been automatically upgraded. Please run it again."
    else
      rm -f bitwarden.sh.1
    fi
  fi
done

if [ $FOUND = false ]
then
  echo "We have moved our self-hosted scripts to their own repository (https://github.com/bitwarden/self-host). Please run 'bitwarden.sh updateself' before updating."
fi
