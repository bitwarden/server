#!/usr/bin/env bash
set -e

echo "Please enter your domain name (i.e. bitwarden.company.com): "
read DOMAIN
echo -e "\nPlease enter your email address (used to generate an HTTPS certificate with LetsEncrypt): "
read EMAIL

OUTPUT_DIR=./bitwarden
DOCKER_DIR=../docker
CERT_PASSWORD=$(LC_ALL=C tr -dc A-Za-z0-9 </dev/urandom | head -c 32)
DATABASE_PASSWORD=$(LC_ALL=C tr -dc A-Za-z0-9 </dev/urandom | head -c 32)
DUO_KEY=$(LC_ALL=C tr -dc A-Za-z0-9 </dev/urandom | head -c 64)

docker --version

#mkdir -p $OUTPUT_DIR/letsencrypt/live/$DOMAIN
#docker run -it --rm -p 80:80 -v $OUTPUT_DIR/letsencrypt:/etc/letsencrypt/ certbot/certbot certonly --standalone --noninteractive --preferred-challenges http --email $EMAIL --agree-tos -d $DOMAIN
#docker run -it --rm -v $OUTPUT_DIR/letsencrypt/live:/certificates/ bitwarden/openssl openssl dhparam -out /certificates/$DOMAIN/dhparam.pem 2048

mkdir -p $OUTPUT_DIR/core
docker run -it --rm -v $OUTPUT_DIR/core:/certificates bitwarden/openssl openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout /certificates/identity.key -out /certificates/identity.crt -subj "/CN=bitwarden IdentityServer" -days 10950
docker run -it --rm -v $OUTPUT_DIR/core:/certificates bitwarden/openssl openssl pkcs12 -export -out /certificates/identity.pfx -inkey /certificates/identity.key -in /certificates/identity.crt -certfile /certificates/identity.crt -passout pass:$CERT_PASSWORD
rm $OUTPUT_DIR/core/identity.key
rm $OUTPUT_DIR/core/identity.crt

cat >> $DOCKER_DIR/global.override.env << EOF
globalSettings:baseServiceUri:vault=https://$DOMAIN
globalSettings:baseServiceUri:api=https://$DOMAIN/api
globalSettings:baseServiceUri:identity=https://$DOMAIN/identity
globalSettings:sqlServer:connectionString=Server=tcp:mssql,1433;Initial Catalog=vault;Persist Security Info=False;User ID=sa;Password=$DATABASE_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Connection Timeout=30;
globalSettings:identityServer:certificatePassword=$CERT_PASSWORD
globalSettings:duo:aKey=$DUO_KEY
globalSettings:yubico:clientId=REPLACE
globalSettings:yubico:REPLACE
EOF

cat >> $DOCKER_DIR/mssql.override.env << EOF
ACCEPT_EULA=Y
MSSQL_PID=Express
SA_PASSWORD=$DATABASE_PASSWORD
EOF