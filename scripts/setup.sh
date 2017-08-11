#!/usr/bin/env bash
set -e

YEAR=$(date +'%Y')

cat << "EOF"
 _     _ _                         _            
| |__ (_) |___      ____ _ _ __ __| | ___ _ __  
| '_ \| | __\ \ /\ / / _` | '__/ _` |/ _ \ '_ \ 
| |_) | | |_ \ V  V / (_| | | | (_| |  __/ | | |
|_.__/|_|\__| \_/\_/ \__,_|_|  \__,_|\___|_| |_|

EOF

cat << EOF
Open source password management solutions
Copyright 2015-$YEAR, 8bit Solutions LLC
https://bitwarden.com, https://github.com/bitwarden

EOF

docker --version
echo ""

OUTPUT_DIR=/etc/bitwarden
mkdir -p $OUTPUT_DIR

echo "(!) Enter your installation id (get it at https://bitwarden.com/host/): "
read INSTALL_ID
echo -e "\n(!) Enter your installation key: "
read INSTALL_KEY
echo -e "\n(!) Enter the domain name for your bitwarden instance (ex. bitwarden.company.com): "
read DOMAIN
echo -e "\n(!) Do you want to use Let's Encrypt to generate a free SSL certificate? (y/n): "
read LETS_ENCRYPT

DATABASE_PASSWORD=$(LC_ALL=C tr -dc A-Za-z0-9 </dev/urandom | head -c 32)

if [ $LETS_ENCRYPT == 'y' ]
then
    echo -e "\n(!) Enter your email address (Let's Encrypt will send you certificate expiration reminders): "
    read EMAIL
    mkdir -p $OUTPUT_DIR/letsencrypt/live/$DOMAIN
    docker run -it --rm --name certbot -p 80:80 -v $OUTPUT_DIR/letsencrypt:/etc/letsencrypt/ certbot/certbot certonly --standalone --noninteractive --preferred-challenges http --email $EMAIL --agree-tos -d $DOMAIN
fi

docker run -it --rm --name setup -v $OUTPUT_DIR:/bitwarden bitwarden/setup dotnet Setup.dll -domain $DOMAIN -letsencrypt $LETS_ENCRYPT -db_pass $DATABASE_PASSWORD -install_id $INSTALL_ID -install_key $INSTALL_KEY

echo -e "\nSetup complete"
