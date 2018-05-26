#!/usr/bin/env bash
set -e

CYAN='\033[0;36m'
NC='\033[0m' # No Color

OUTPUT_DIR=".."
if [ $# -gt 0 ]
then
    OUTPUT_DIR=$1
    mkdir -p $OUTPUT_DIR
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

ENV_DIR="$OUTPUT_DIR/env"

# Save the installation UID/GID, they could also be manually changed later on if desired
LUID="LOCAL_UID=`id -u $USER`"
LGID="LOCAL_GID=`id -g $USER`"
mkdir -p $ENV_DIR
(echo $LUID; echo $LGID) > $ENV_DIR/uid.env

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
docker run -it --rm --name setup -v $OUTPUT_DIR:/bitwarden --env-file $ENV_DIR/uid.env bitwarden/setup:$COREVERSION \
    dotnet Setup.dll -install 1 -domain $DOMAIN -letsencrypt $LETS_ENCRYPT -os $OS -corev $COREVERSION -webv $WEBVERSION

echo ""
echo "Setup complete"
echo ""
