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

OUTPUT_DIR=~/bitwarden
mkdir -p $OUTPUT_DIR

read -p "(!) Enter the domain name for your bitwarden instance (ex. bitwarden.company.com): " DOMAIN
read -p "(!) Do you want to use Let's Encrypt to generate a free SSL certificate? (y/n): " LETS_ENCRYPT

if [ $LETS_ENCRYPT == 'y' ]
then
    read -p "(!) Enter your email address (Let's Encrypt will send you certificate expiration reminders): " EMAIL
    mkdir -p $OUTPUT_DIR/letsencrypt/live/$DOMAIN
    docker run -it --rm --name certbot -p 80:80 -v $OUTPUT_DIR/letsencrypt:/etc/letsencrypt/ certbot/certbot \
        certonly --standalone --noninteractive  --agree-tos --preferred-challenges http --email $EMAIL -d $DOMAIN
fi

docker run -it --rm --name setup -v $OUTPUT_DIR:/bitwarden bitwarden/setup \
    dotnet Setup.dll -install 1 -domain $DOMAIN -letsencrypt $LETS_ENCRYPT

echo ""
echo "Setup complete"
