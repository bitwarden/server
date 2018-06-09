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
    echo -e -n "${CYAN}(!)${NC} Enter the domain name for your Bitwarden instance (ex. bitwarden.company.com): "
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
    
    echo ""
    echo "Setup complete"
    echo ""
}

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
    # Manually rotate previous sqlagent logfile so that waitForDB() will not match against it
    if [ -f $OUTPUT_DIR/logs/mssql/sqlagent.out ]
    then
        mv $OUTPUT_DIR/logs/mssql/sqlagent.out $OUTPUT_DIR/logs/mssql/sqlagent.old
    fi
}

function dockerComposePull() {
    if [ -f "${DOCKER_DIR}/docker-compose.override.yml" ]
    then
        docker-compose -f $DOCKER_DIR/docker-compose.yml -f $DOCKER_DIR/docker-compose.override.yml pull $1
    else
        docker-compose -f $DOCKER_DIR/docker-compose.yml pull $1
    fi
}

function dockerPrune() {
    docker image prune -f
    # Perhaps we could prefer the following, to recover disk space after automatic update ?
    # docker image prune -f -a --filter="label=com.bitwarden.product=bitwarden"
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

function pullSetup() {
    docker pull bitwarden/setup:$COREVERSION
}

function waitForDB() {
    i=0
    echo -n "Pausing for database to come online. Please wait..."
    while ! sed 's/[^a-zA-Z ]//g' $OUTPUT_DIR/logs/mssql/sqlagent.out 2>/dev/null | tr '\n' ' ' | grep -iq "Waiting for SQL Server to start .* SQLServerAgent service successfully started"
    do
        sleep 2
        i=$(($i+2))
        echo -n .
    done
    echo " ($i s)"
}

# Commands

if [ "$1" == "install" ]
then
    install
elif [ "$1" == "start" -o "$1" == "restart" ]
then
    dockerComposeDown
    dockerComposePull
    updateLetsEncrypt
    dockerComposeUp mssql
    waitForDB
    dockerComposeUp
    printEnvironment
elif [ "$1" == "stop" ]
then
    dockerComposeDown
elif [ "$1" == "updateapp" ]
then
    dockerComposeDown
    update
    dockerComposePull
    updateLetsEncrypt
elif [ "$1" == "updatedb" ]
then
    dockerComposeDown
    dockerComposePull mssql
    dockerComposeUp mssql
    waitForDB
    updateDatabase
elif [ "$1" == "update" ]
then
    dockerComposeDown
    update
    dockerComposePull
    updateLetsEncrypt
    dockerComposeUp mssql
    waitForDB
    updateDatabase
    dockerComposeUp
    dockerPrune
    printEnvironment
fi
