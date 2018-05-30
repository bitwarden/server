#!/usr/bin/env bash
set -e

# Setup

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

OUTPUT_DIR=".."
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

OS="nix"
ENV_DIR="$OUTPUT_DIR/env"
DOCKER_DIR="$OUTPUT_DIR/docker"

# Initialize UID/GID which will be used to run services
if ! grep -q "^LOCAL_UID=" $ENV_DIR/uid.env 2>/dev/null || ! grep -q "^LOCAL_GID=" $ENV_DIR/uid.env 2>/dev/null
then
    LUID="LOCAL_UID=`id -u $USER`"
    LGID="LOCAL_GID=`id -g $USER`"
    mkdir -p $ENV_DIR
    echo $LUID >$ENV_DIR/uid.env
    echo $LGID >>$ENV_DIR/uid.env
fi

# Up to Core version 1.19.0, UID/GID given by the user in uid.env may collide with existing UID/GID in images
# We then must enforce a UID/GID pair known to be available in images
# Newer versions properly use -o switch to useradd / groupadd to avoid this problem
if [[ "$COREVERSION" == *.*.* ]] &&
   echo -e "1.19.0\n$COREVERSION" | sort -t '.' -k 1,1 -k 2,2 -k 3,3 -n | awk 'END {if($0!="1.19.0") {exit 1}}'
then
    LUID="LOCAL_UID=999"
    LGID="LOCAL_GID=999"
    echo $LUID >$ENV_DIR/uid.env
    echo $LGID >>$ENV_DIR/uid.env
fi

# Functions

function dockerComposeUp() {
    if [ -f "${DOCKER_DIR}/docker-compose.override.yml" ]
    then
        docker-compose -f $DOCKER_DIR/docker-compose.yml -f $DOCKER_DIR/docker-compose.override.yml up -d
    else
        docker-compose -f $DOCKER_DIR/docker-compose.yml up -d
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
    docker run -i --rm --name setup --network container:bitwarden-mssql \
        -v $OUTPUT_DIR:/bitwarden --env-file $ENV_DIR/uid.env bitwarden/setup:$COREVERSION \
        dotnet Setup.dll -update 1 -db 1 -os $OS -corev $COREVERSION -webv $WEBVERSION
    echo "Database update complete"
}

function update() {
    pullSetup
    docker run -i --rm --name setup -v $OUTPUT_DIR:/bitwarden \
        --env-file $ENV_DIR/uid.env bitwarden/setup:$COREVERSION \
        dotnet Setup.dll -update 1 -os $OS -corev $COREVERSION -webv $WEBVERSION
}

function printEnvironment() {
    pullSetup
    docker run -i --rm --name setup -v $OUTPUT_DIR:/bitwarden \
        --env-file $ENV_DIR/uid.env bitwarden/setup:$COREVERSION \
        dotnet Setup.dll -printenv 1 -os $OS -corev $COREVERSION -webv $WEBVERSION
}

function restart() {
    dockerComposeDown
    dockerComposePull
    updateLetsEncrypt
    dockerComposeUp
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
elif [ "$1" == "pull" ]
then
    dockerComposePull
elif [ "$1" == "stop" ]
then
    dockerComposeDown
elif [ "$1" == "updatedb" ]
then
    updateDatabase
elif [ "$1" == "update" ]
then
    dockerComposeDown
    update
    restart
    echo "Pausing 60 seconds for database to come online. Please wait..."
    sleep 60
    updateDatabase
fi
