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

EOF

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
OUTPUT="$DIR/bitwarden"
if [ $# -eq 2 ]
then
    OUTPUT=$2
fi

if [ ! -d "$OUTPUT" ]
then
    mkdir $OUTPUT
fi

OS="linux"
if [ "$(uname)" == "Darwin" ]
then
    OS="macwin"
fi

SCRIPTS_DIR="$OUTPUT/scripts"
DOCKER_DIR="$OUTPUT/docker"
GITHUB_BASE_URL="https://raw.githubusercontent.com/bitwarden/core/master"

if [ ! -d "$SCRIPTS_DIR" ]
then
    mkdir $SCRIPTS_DIR
fi

function downloadRunFiles() {
    curl -s -o $SCRIPTS_DIR/run.sh $GITHUB_BASE_URL/scripts/run.sh
    chmod u+x $SCRIPTS_DIR/run.sh
    curl -s -o $SCRIPTS_DIR/stop.sh $GITHUB_BASE_URL/scripts/stop.sh
    chmod u+x $SCRIPTS_DIR/stop.sh
    curl -s -o $DOCKER_DIR/docker-compose.yml $GITHUB_BASE_URL/docker/docker-compose.yml
    curl -s -o $DOCKER_DIR/docker-compose.$OS.yml $GITHUB_BASE_URL/docker/docker-compose.$OS.yml
    curl -s -o $DOCKER_DIR/global.env $GITHUB_BASE_URL/docker/global.env
    curl -s -o $DOCKER_DIR/mssql.env $GITHUB_BASE_URL/docker/mssql.env
}

if [ "$1" == "install" ]
then
    curl -s -o $SCRIPTS_DIR/install.sh $GITHUB_BASE_URL/scripts/install.sh
    chmod u+x $SCRIPTS_DIR/install.sh
    $SCRIPTS_DIR/install.sh $OUTPUT
elif [ "$1" == "run" -o "$1" == "restart" ]
then
    if [ ! -d "$DOCKER_DIR" ]
    then
        mkdir $DOCKER_DIR
        downloadRunFiles
    fi
    $SCRIPTS_DIR/run.sh $OUTPUT $DOCKER_DIR
elif [ "$1" == "update" ]
then
    if [ -d "$DOCKER_DIR" ]
    then
        rm -rf $DOCKER_DIR
    fi

    mkdir $DOCKER_DIR
    downloadRunFiles
    $SCRIPTS_DIR/run.sh $OUTPUT $DOCKER_DIR
elif [ "$1" == "updatedb" ]
then
    curl -s -o $SCRIPTS_DIR/update-db.sh $GITHUB_BASE_URL/scripts/update-db.sh
    chmod u+x $SCRIPTS_DIR/update-db.sh
    $SCRIPTS_DIR/update-db.sh $OUTPUT
elif [ "$1" == "stop" ]
then
    $SCRIPTS_DIR/stop.sh $DOCKER_DIR
else
    echo "No command found."
fi
