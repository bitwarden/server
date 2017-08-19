#!/usr/bin/env bash
set -e

YEAR=$(date +'%Y')

cat << "EOF"
 _     _ _                         _            
| |__ (_) |___      ____ _ _ __ __| | ___ _ __  
| '_ \| | __\ \ /\ / / _` | '__/ _` |/ _ \ '_ \ 
| |_) | | |_ \ V  V / (_| | | | (_| |  __/ | | |
|_.__/|_|\__| \_/\_/ \__,_|_|  \__,_|\___|_| |_|

EOF

cat << EOF
Open source password management solutions
Copyright 2015-$YEAR, 8bit Solutions LLC
https://bitwarden.com, https://github.com/bitwarden

EOF

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
DOCKER_DIR="./docker"
GITHUB_BASE_URL="https://raw.githubusercontent.com/bitwarden/core/master"
OUTPUT=~/bitwarden
if [ $# -eq 2 ]
then
    OUTPUT=$2
fi

function dowloadRunFiles {
    curl -s -o run.sh $GITHUB_BASE_URL/scripts/run.ps1
    curl -s -o $DOCKER_DIR/docker-compose.yml $GITHUB_BASE_URL/docker/docker-compose.yml
    curl -s -o $DOCKER_DIR/docker-compose.linux.yml $GITHUB_BASE_URL/docker/docker-compose.linux.yml
    curl -s -o $DOCKER_DIR/global.env $GITHUB_BASE_URL/docker/global.env
    curl -s -o $DOCKER_DIR/mssql.env $GITHUB_BASE_URL/docker/mssql.env
}

if [ $1 == 'install' ]
then
    curl -s -o install.sh $GITHUB_BASE_URL/scripts/install.sh
    chmod u+x install.sh
    ./install.sh $OUTPUT
elif [ $1 == 'run' -o $1 == 'restart' ]
then
    #
elif [ $1 == 'update' ]
then
    #
elif [ $1 == 'updatedb' ]
then
    curl -s -o update-db.sh $GITHUB_BASE_URL/scripts/update-db.sh
    chmod u+x update-db.sh
    ./update-db.sh $OUTPUT
else
    echo "No command found."
fi
