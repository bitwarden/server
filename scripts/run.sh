#!/usr/bin/env bash
set -e

# Setup

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

OUTPUT_DIR="../."
if [ $# -gt 1 ]
then
    OUTPUT_DIR=$2
fi

DOCKER_DIR=$DIR/../docker
if [ $# -gt 2 ]
then
    DOCKER_DIR=$3
fi

OS="linwin"
if [ "$(uname)" == "Darwin" ]
then
    OS="mac"
fi

TAG="1.12.0"

# Functions

function dockerComposeUp() {
    docker-compose -f $DOCKER_DIR/docker-compose.yml -f $DOCKER_DIR/docker-compose.$OS.yml up -d
}

function dockerComposeDown() {
    docker-compose -f $DOCKER_DIR/docker-compose.yml -f $DOCKER_DIR/docker-compose.$OS.yml down
}

function dockerComposePull() {
    docker-compose -f $DOCKER_DIR/docker-compose.yml -f $DOCKER_DIR/docker-compose.$OS.yml pull
}

function dockerPrune() {
    docker image prune -f
}

function updateLetsEncrypt() {
    if [ -d "${outputDir}/letsencrypt/live" ]
    then
        docker pull certbot/certbot
        docker run -it --rm --name certbot -p 443:443 -p 80:80 -v $OUTPUT_DIR/letsencrypt:/etc/letsencrypt/ certbot/certbot \
            renew --logs-dir /etc/letsencrypt/logs
    fi
}

function updateDatabase() {
    docker pull bitwarden/setup:$TAG
    docker run -it --rm --name setup --network container:mssql -v $OUTPUT_DIR:/bitwarden bitwarden/setup:$TAG \
        dotnet Setup.dll -update 1 -db 1
    echo "Database update complete"
}

function printEnvironment() {
    docker pull bitwarden/setup:$TAG
    docker run -it --rm --name setup -v $OUTPUT_DIR:/bitwarden bitwarden/setup:$TAG \
        dotnet Setup.dll -printenv 1 -env $OS
}

# Commands

if [ "$1" == "start" -o "$1" == "restart" ]
then
    dockerComposeDown
    dockerComposePull
    updateLetsEncrypt
    dockerComposeUp
    dockerPrune
    printEnvironment
elif [ "$1" == "pull" ]
then
    dockerComposePull
elif [ "$1" == "stop" ]
then
    dockerComposeDown
elif [ "$1" == "updatedb" ]
then
    updateDatabase
fi
