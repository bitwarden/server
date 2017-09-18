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

docker --version
docker-compose --version

echo ""

# Setup

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
SCRIPT_NAME=`basename "$0"`
SCRIPT_PATH="$DIR/$SCRIPT_NAME"
OUTPUT="$DIR/bitwarden"
if [ $# -eq 2 ]
then
    OUTPUT=$2
fi

OS="linux"
if [ "$(uname)" == "Darwin" ]
then
    OS="macwin"
fi

SCRIPTS_DIR="$OUTPUT/scripts"
DOCKER_DIR="$OUTPUT/docker"
GITHUB_BASE_URL="https://raw.githubusercontent.com/bitwarden/core/master"

# Functions

function downloadSelf() {
    if [ ! -d "$SCRIPTS_DIR" ]
    then
        mkdir $SCRIPTS_DIR
    fi
    curl -s -o $SCRIPT_PATH $GITHUB_BASE_URL/scripts/bitwarden.sh
    chmod u+x $SCRIPT_PATH
}

function downloadInstall() {
    if [ ! -d "$SCRIPTS_DIR" ]
    then
        mkdir $SCRIPTS_DIR
    fi
    curl -s -o $SCRIPTS_DIR/install.sh $GITHUB_BASE_URL/scripts/install.sh
    chmod u+x $SCRIPTS_DIR/install.sh
}

function downloadRunFile() {
    if [ ! -d "$SCRIPTS_DIR" ]
    then
        mkdir $SCRIPTS_DIR
    fi
    curl -s -o $SCRIPTS_DIR/run.sh $GITHUB_BASE_URL/scripts/run.sh
    chmod u+x $SCRIPTS_DIR/run.sh
}

function downloadDockerFiles() {
    curl -s -o $DOCKER_DIR/docker-compose.yml $GITHUB_BASE_URL/docker/docker-compose.yml
    curl -s -o $DOCKER_DIR/docker-compose.$OS.yml $GITHUB_BASE_URL/docker/docker-compose.$OS.yml
    curl -s -o $DOCKER_DIR/global.env $GITHUB_BASE_URL/docker/global.env
    curl -s -o $DOCKER_DIR/mssql.env $GITHUB_BASE_URL/docker/mssql.env
}

function downloadAllFiles() {
    downloadRunFile
    downloadDockerFiles
}

function checkOutputDirExists() {
    if [ ! -d "$OUTPUT" ]
    then
        echo "Cannot find a bitwarden installation at $OUTPUT."
        exit 1
    fi
}

function checkOutputDirNotExists() {
    if [ -d "$OUTPUT" ]
    then
        echo "Looks like bitwarden is already installed at $OUTPUT."
        exit 1
    fi
}

# Commands

if [ "$1" == "install" ]
then
    checkOutputDirNotExists
    mkdir $OUTPUT
    downloadInstall
    $SCRIPTS_DIR/install.sh $OUTPUT
elif [ "$1" == "start" -o "$1" == "restart" ]
then
    checkOutputDirExists
    if [ ! -d "$DOCKER_DIR" ]
    then
        mkdir $DOCKER_DIR
        downloadAllFiles
    fi
    
    $SCRIPTS_DIR/run.sh restart $OUTPUT $DOCKER_DIR
elif [ "$1" == "update" ]
then
    checkOutputDirExists
    if [ -d "$DOCKER_DIR" ]
    then
        rm -rf $DOCKER_DIR
    fi

    mkdir $DOCKER_DIR
    downloadAllFiles
    $SCRIPTS_DIR/run.sh restart $OUTPUT $DOCKER_DIR
    $SCRIPTS_DIR/run.sh updatedb $OUTPUT $DOCKER_DIR
elif [ "$1" == "updatedb" ]
then
    checkOutputDirExists
    $SCRIPTS_DIR/run.sh updatedb $OUTPUT $DOCKER_DIR
elif [ "$1" == "stop" ]
then
    checkOutputDirExists
    $SCRIPTS_DIR/run.sh stop $OUTPUT $DOCKER_DIR
elif [ "$1" == "updateself" ]
then
    checkOutputDirExists
    downloadSelf
    echo "Updated self."
else
    echo "No command found."
fi
