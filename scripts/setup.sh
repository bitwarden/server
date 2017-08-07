#!/usr/bin/env bash
set -e

echo "Please enter your domain name (i.e. bitwarden.company.com): "
read DOMAIN
echo -e "\nPlease enter your email address (used to generate an HTTPS certificate with LetsEncrypt): "
read EMAIL

OUTPUT_DIR=./bitwarden
DATABASE_PASSWORD=$(LC_ALL=C tr -dc A-Za-z0-9 </dev/urandom | head -c 32)

docker --version

#mkdir -p $OUTPUT_DIR/letsencrypt/live/$DOMAIN
#docker run -it --rm -p 80:80 -v $OUTPUT_DIR/letsencrypt:/etc/letsencrypt/ certbot/certbot certonly --standalone --noninteractive --preferred-challenges http --email $EMAIL --agree-tos -d $DOMAIN
#docker run -it --rm -v $OUTPUT_DIR/letsencrypt/live:/certificates/ bitwarden/openssl openssl dhparam -out /certificates/$DOMAIN/dhparam.pem 2048

docker run -it --rm -v $OUTPUT_DIR:/bitwarden bitwarden/setup dotnet Setup.dll -domain $DOMAIN -letsencrypt y -db_pass $DATABASE_PASSWORD

echo -e "\nSetup complete"
