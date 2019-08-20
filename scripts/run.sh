#!/usr/bin/env bash
set -e

# Setup

CYAN='\033[0;36m'
NC='\033[0m' # No Color

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

OS="lin"
[ "$(uname)" == "Darwin" ] && OS="mac"
ENV_DIR="$OUTPUT_DIR/env"
DOCKER_DIR="$OUTPUT_DIR/docker"

# Initialize UID/GID which will be used to run services from within containers
if ! grep -q "^LOCAL_UID=" $ENV_DIR/uid.env 2>/dev/null || ! grep -q "^LOCAL_GID=" $ENV_DIR/uid.env 2>/dev/null
then
    LUID="LOCAL_UID=`id -u $USER`"
    [ "$LUID" == "LOCAL_UID=0" ] && LUID="LOCAL_UID=65534"
    LGID="LOCAL_GID=`id -g $USER`"
    [ "$LGID" == "LOCAL_GID=0" ] && LGID="LOCAL_GID=65534"
    mkdir -p $ENV_DIR
    echo $LUID >$ENV_DIR/uid.env
    echo $LGID >>$ENV_DIR/uid.env
fi

# Backwards compat GID/UID for pre-1.20.0 installations
if [[ "$COREVERSION" == *.*.* ]] &&
   echo -e "1.19.0\n$COREVERSION" | sort -t '.' -k 1,1 -k 2,2 -k 3,3 -n | awk 'END {if($0!="1.19.0") {exit 1}}'
then
    LUID="LOCAL_UID=`id -u $USER`"
    LGID="LOCAL_GID=`awk -F: '$1=="docker" {print $3}' /etc/group`"
    if [ "$OS" == "mac" ]
    then
        LUID="LOCAL_UID=999"
        LGID="LOCAL_GID=999"
    fi
    echo $LUID >$ENV_DIR/uid.env
    echo $LGID >>$ENV_DIR/uid.env
fi

# Functions

function install() {
    LETS_ENCRYPT="n"
    echo -e -n "${CYAN}(!)${NC} Enter the domain name for your Bitwarden instance (ex. bitwarden.example.com): "
    read DOMAIN
    echo ""
    
    if [ "$DOMAIN" == "" ]
    then
        DOMAIN="localhost"
    fi
    
    if [ "$DOMAIN" != "localhost" ]
    then
        echo -e -n "${CYAN}(!)${NC} Do you want to use Let's Encrypt to generate a free SSL certificate? (y/n): "
        read LETS_ENCRYPT
        echo ""
    
        if [ "$LETS_ENCRYPT" == "y" ]
        then
            echo -e -n "${CYAN}(!)${NC} Enter your email address (Let's Encrypt will send you certificate expiration reminders): "
            read EMAIL
            echo ""
    
            mkdir -p $OUTPUT_DIR/letsencrypt
            docker pull certbot/certbot
            docker run -it --rm --name certbot -p 80:80 -v $OUTPUT_DIR/letsencrypt:/etc/letsencrypt/ certbot/certbot \
                certonly --standalone --noninteractive  --agree-tos --preferred-challenges http \
                --email $EMAIL -d $DOMAIN --logs-dir /etc/letsencrypt/logs
        fi
    fi
    
    pullSetup
    docker run -it --rm --name setup -v $OUTPUT_DIR:/bitwarden \
        --env-file $ENV_DIR/uid.env bitwarden/setup:$COREVERSION \
        dotnet Setup.dll -install 1 -domain $DOMAIN -letsencrypt $LETS_ENCRYPT -os $OS \
        -corev $COREVERSION -webv $WEBVERSION
}

function dockerComposeUp() {
    dockerComposeFiles
    docker-compose up -d
}

function dockerComposeDown() {
    dockerComposeFiles
    docker-compose down
}

function dockerComposePull() {
    dockerComposeFiles
    docker-compose pull
}

function dockerComposeFiles() {
    if [ -f "${DOCKER_DIR}/docker-compose.override.yml" ]
    then
        export COMPOSE_FILE="$DOCKER_DIR/docker-compose.yml:$DOCKER_DIR/docker-compose.override.yml"
    else
        export COMPOSE_FILE="$DOCKER_DIR/docker-compose.yml"
    fi
    export COMPOSE_HTTP_TIMEOUT="300"
}

function dockerPrune() {
    docker image prune --all --force --filter="label=com.bitwarden.product=bitwarden" \
        --filter="label!=com.bitwarden.project=setup"
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
    if [ "$1" == "withpull" ]
    then
        pullSetup
    fi
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

if [ "$1" == "install" ]
then
    install
elif [ "$1" == "start" -o "$1" == "restart" ]
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
    update withpull
    restart
    echo "Pausing 60 seconds for database to come online. Please wait..."
    sleep 60
    updateDatabase
elif [ "$1" == "rebuild" ]
then
    dockerComposeDown
    update nopull
fi
