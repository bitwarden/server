#!/usr/bin/env bash
set -e

docker --version
echo ""

OUTPUT_DIR="../."
if [ $# -eq 1 ]
then
    OUTPUT_DIR=$1
fi
mkdir -p $OUTPUT_DIR

LETS_ENCRYPT="n"
read -p "(!) Enter the domain name for your bitwarden instance (ex. bitwarden.company.com): " DOMAIN

if [ "$DOMAIN" != "localhost" ]
then
    read -p "(!) Do you want to use Let's Encrypt to generate a free SSL certificate? (y/n): " LETS_ENCRYPT

    if [ "$LETS_ENCRYPT" == "y" ]
    then
        read -p "(!) Enter your email address (Let's Encrypt will send you certificate expiration reminders): " EMAIL
        mkdir -p $OUTPUT_DIR/letsencrypt/live/$DOMAIN
        docker run -it --rm --name certbot -p 80:80 -v $OUTPUT_DIR/letsencrypt:/etc/letsencrypt/ certbot/certbot \
            certonly --standalone --noninteractive  --agree-tos --preferred-challenges http --email $EMAIL -d $DOMAIN
    fi
fi

docker run -it --rm --name setup -v $OUTPUT_DIR:/bitwarden bitwarden/setup \
    dotnet Setup.dll -install 1 -domain $DOMAIN -letsencrypt $LETS_ENCRYPT

echo ""
echo "Setup complete"
