#!/usr/bin/env bash
set -e

# Setup

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

OUTPUT_DIR="../."
if [ $# -gt 1 ]
then
    OUTPUT_DIR=$2
fi

COREVERSION="latest"
if [ $# -gt 2 ]
then
    COREVERSION=$3
fi

WEBVERSION="latest"
if [ $# -gt 3 ]
then
    WEBVERSION=$4
fi

OS="lin"
if [ "$(uname)" == "Darwin" ]
then
    OS="mac"
fi

DOCKER_DIR="$OUTPUT_DIR/docker"
ENV_DIR="$OUTPUT_DIR/env"
LUID="LOCAL_UID=`id -u $USER`"
LGID="LOCAL_GID=`getent group docker | cut -d: -f3`"

# Functions

function dockerComposeUp() {
    if [ -f "${DOCKER_DIR}/docker-compose.override.yml" ]
    then
        docker-compose -f $DOCKER_DIR/docker-compose.yml -f $DOCKER_DIR/docker-compose.override.yml up -d $1
    else
        docker-compose -f $DOCKER_DIR/docker-compose.yml up -d $1
    fi
}

function dockerComposeDown() {
    if [ -f "${DOCKER_DIR}/docker-compose.override.yml" ]
    then
        docker-compose -f $DOCKER_DIR/docker-compose.yml -f $DOCKER_DIR/docker-compose.override.yml down
    else
        docker-compose -f $DOCKER_DIR/docker-compose.yml down
    fi
}

function dockerComposePull() {
    if [ -f "${DOCKER_DIR}/docker-compose.override.yml" ]
    then
        docker-compose -f $DOCKER_DIR/docker-compose.yml -f $DOCKER_DIR/docker-compose.override.yml pull
    else
        docker-compose -f $DOCKER_DIR/docker-compose.yml pull
    fi
}

function dockerPrune() {
    docker image prune -f
}

function updateLetsEncrypt() {
    if [ -d "${OUTPUT_DIR}/letsencrypt/live" ]
    then
        docker pull certbot/certbot
        docker run -i --rm --name certbot -p 443:443 -p 80:80 \
            -v $OUTPUT_DIR/letsencrypt:/etc/letsencrypt/ certbot/certbot \
            renew --logs-dir /etc/letsencrypt/logs
    fi
}

function updateDatabase() {
    pullSetup
    if [ $OS == "lin" ]
    then
        docker run -i --rm --name setup --network container:bitwarden-mssql \
            -v $OUTPUT_DIR:/bitwarden -e $LUID -e $LGID bitwarden/setup:$COREVERSION \
            dotnet Setup.dll -update 1 -db 1 -os $OS -corev $COREVERSION -webv $WEBVERSION
    else
        docker run -i --rm --name setup --network container:bitwarden-mssql \
            -v $OUTPUT_DIR:/bitwarden bitwarden/setup:$COREVERSION \
            dotnet Setup.dll -update 1 -db 1 -os $OS -corev $COREVERSION -webv $WEBVERSION
    fi
    echo "Database update complete"
}

function update() {
    pullSetup
    if [ $OS == "lin" ]
    then
        docker run -i --rm --name setup -v $OUTPUT_DIR:/bitwarden \
            -e $LUID -e $LGID bitwarden/setup:$COREVERSION \
            dotnet Setup.dll -update 1 -os $OS -corev $COREVERSION -webv $WEBVERSION
    else
        docker run -i --rm --name setup \
            -v $OUTPUT_DIR:/bitwarden bitwarden/setup:$COREVERSION \
            dotnet Setup.dll -update 1 -os $OS -corev $COREVERSION -webv $WEBVERSION
    fi
}

function printEnvironment() {
    pullSetup
    if [ $OS == "lin" ]
    then
        docker run -i --rm --name setup -v $OUTPUT_DIR:/bitwarden \
            -e $LUID -e $LGID bitwarden/setup:$COREVERSION \
            dotnet Setup.dll -printenv 1 -os $OS -corev $COREVERSION -webv $WEBVERSION
    else
        docker run -i --rm --name setup \
            -v $OUTPUT_DIR:/bitwarden bitwarden/setup:$COREVERSION \
            dotnet Setup.dll -printenv 1 -os $OS -corev $COREVERSION -webv $WEBVERSION
    fi
}

function restart() {
    dockerComposeDown
    dockerComposePull
    updateLetsEncrypt
    
    if [ $OS == "lin" ]
    then
        mkdir -p $ENV_DIR
        (echo $LUID; echo $LGID) > $ENV_DIR/uid.env
    fi
    
    dockerComposeUp $1
    dockerPrune
    printEnvironment
}

function pullSetup() {
    docker pull bitwarden/setup:$COREVERSION
}

# Commands

if [ "$1" == "start" -o "$1" == "restart" ]
then
    restart
elif [ "$1" == "stop" ]
then
    dockerComposeDown
elif [ "$1" == "updateapp" ]
then
    dockerComposeDown
    update
elif [ "$1" == "updatedb" ]
then
    restart mssql
    echo "Pausing 60 seconds for database to come online. Please wait..."
    sleep 60
    updateDatabase
elif [ "$1" == "update" ]
then
    dockerComposeDown
    update
    restart mssql
    echo "Pausing 60 seconds for database to come online. Please wait..."
    sleep 60
    updateDatabase
    restart
fi
