#!/usr/bin/env bash
set -e

CYAN='\033[0;36m'
NC='\033[0m' # No Color

OUTPUT_DIR="../."
if [ $# -gt 0 ]
then
    OUTPUT_DIR=$1
fi

COREVERSION="latest"
if [ $# -gt 1 ]
then
    COREVERSION=$2
fi

WEBVERSION="latest"
if [ $# -gt 2 ]
then
    WEBVERSION=$3
fi

OS="lin"
if [ "$(uname)" == "Darwin" ]
then
    OS="mac"
fi

LUID="LOCAL_UID=`id -u $USER`"
if [ "$OS" == "lin" -a `id -u $USER` -eq 0 ]
then
    LGID="LOCAL_GID=`getent group docker | cut -d: -f3`"
else
    LGID="LOCAL_GID=`id -g $USER`"
fi

mkdir -p $OUTPUT_DIR

LETS_ENCRYPT="n"
echo -e -n "${CYAN}(!)${NC} Enter the domain name for your bitwarden instance (ex. bitwarden.company.com): "
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
            certonly --standalone --noninteractive  --agree-tos --preferred-challenges http --email $EMAIL -d $DOMAIN \
            --logs-dir /etc/letsencrypt/logs
    fi
fi

docker pull bitwarden/setup:$COREVERSION
if [ $OS == "lin" ]
then
    docker run -it --rm --name setup -v $OUTPUT_DIR:/bitwarden -e $LUID -e $LGID bitwarden/setup:$COREVERSION \
        dotnet Setup.dll -install 1 -domain $DOMAIN -letsencrypt $LETS_ENCRYPT -os $OS -corev $COREVERSION -webv $WEBVERSION
else
    docker run -it --rm --name setup -v $OUTPUT_DIR:/bitwarden bitwarden/setup:$COREVERSION \
        dotnet Setup.dll -install 1 -domain $DOMAIN -letsencrypt $LETS_ENCRYPT -os $OS -corev $COREVERSION -webv $WEBVERSION
fi

echo ""
echo "Setup complete"
echo ""
