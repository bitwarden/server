#!/usr/bin/env bash
set -e

OUTPUT_DIR=/etc/bitwarden
mkdir -p $OUTPUT_DIR

docker --version

echo "Enter the domain name for bitwarden (ex. bitwarden.company.com): "
read DOMAIN
echo -e "\nDo you want to use Let's Encrypt to generate a free SSL certificate? (y/n): "
read LETS_ENCRYPT

DATABASE_PASSWORD=$(LC_ALL=C tr -dc A-Za-z0-9 </dev/urandom | head -c 32)

if [ $LETS_ENCRYPT == 'y' ]
then
    echo -e "\nEnter your email address (Let's Encrypt will send you certificate expiration reminders): "
    read EMAIL
    mkdir -p $OUTPUT_DIR/letsencrypt/live/$DOMAIN
    docker run -it --rm -p 80:80 -v $OUTPUT_DIR/letsencrypt:/etc/letsencrypt/ certbot/certbot certonly --standalone --noninteractive --preferred-challenges http --email $EMAIL --agree-tos -d $DOMAIN
fi

docker run -it --rm -v $OUTPUT_DIR:/bitwarden bitwarden/setup dotnet Setup.dll -domain $DOMAIN -letsencrypt $LETS_ENCRYPT -db_pass $DATABASE_PASSWORD

echo -e "\nSetup complete"
