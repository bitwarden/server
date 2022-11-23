#!/bin/sh

# Translate environment variables for application settings
VAULT_SERVICE_URI=https://$BW_DOMAIN
MYSQL_CONNECTION_STRING="server=$BW_DB_SERVER;database=$BW_DB_DATABASE;user=$BW_DB_USERNAME;password=$BW_DB_PASSWORD"
POSTGRESQL_CONNECTION_STRING="Host=$BW_DB_SERVER;Database=$BW_DB_DATABASE;Username=$BW_DB_USERNAME;Password=$BW_DB_PASSWORD"
SQLSERVER_CONNECTION_STRING="Server=$BW_DB_SERVER;Database=$BW_DB_DATABASE;User Id=$BW_DB_USERNAME;Password=$BW_DB_PASSWORD;"
INTERNAL_IDENTITY_KEY=$(openssl rand -hex 30)
OIDC_IDENTITY_CLIENT_KEY=$(openssl rand -hex 30)
DUO_AKEY=$(openssl rand -hex 30)

export globalSettings__baseServiceUri__vault=${globalSettings__baseServiceUri__vault:-$VAULT_SERVICE_URI}
export globalSettings__installation__id=$BW_INSTALLATION_ID
export globalSettings__installation__key=$BW_INSTALLATION_KEY
export globalSettings__internalIdentityKey=${globalSettings__internalIdentityKey:-$INTERNAL_IDENTITY_KEY}
export globalSettings__oidcIdentityClientKey=${globalSettings__oidcIdentityClientKey:-$OIDC_IDENTITY_CLIENT_KEY}
export globalSettings__duo__aKey=${globalSettings__duo__aKey:-$DUO_AKEY}

export globalSettings__databaseProvider=$BW_DB_PROVIDER
export globalSettings__mysql__connectionString=${globalSettings__mysql__connectionString:-$MYSQL_CONNECTION_STRING}
export globalSettings__postgreSql__connectionString=${globalSettings__postgreSql__connectionString:-$POSTGRESQL_CONNECTION_STRING}
export globalSettings__sqlServer__connectionString=${globalSettings__sqlServer__connectionString:-$SQLSERVER_CONNECTION_STRING}

# Generate Identity certificate
if [ ! -f /etc/bitwarden/identity.pfx ]; then
  openssl req \
  -x509 \
  -newkey rsa:4096 \
  -sha256 \
  -nodes \
  -keyout /etc/bitwarden/identity.key \
  -out /etc/bitwarden/identity.crt \
  -subj "/CN=Bitwarden IdentityServer" \
  -days 36500
  
  openssl pkcs12 \
  -export \
  -out /etc/bitwarden/identity.pfx \
  -inkey /etc/bitwarden/identity.key \
  -in /etc/bitwarden/identity.crt \
  -passout pass:$globalSettings__identityServer__certificatePassword
  
  rm /etc/bitwarden/identity.crt
  rm /etc/bitwarden/identity.key
fi

cp /etc/bitwarden/identity.pfx /app/Identity/identity.pfx
cp /etc/bitwarden/identity.pfx /app/Sso/identity.pfx

# Generate SSL certificates
if [ "$BW_ENABLE_SSL" == "true" -a ! -f /etc/bitwarden/ssl.key ]; then
  openssl req \
  -x509 \
  -newkey rsa:4096 \
  -sha256 \
  -nodes \
  -days 36500 \
  -keyout /etc/bitwarden/${BW_SSL_KEY:-ssl.key} \
  -out /etc/bitwarden/${BW_SSL_CERT:-ssl.crt} \
  -reqexts SAN \
  -extensions SAN \
  -config <(cat /etc/ssl/openssl.cnf <(printf "[SAN]\nsubjectAltName=DNS:${BW_DOMAIN:-localhost}\nbasicConstraints=CA:true")) \
  -subj "/C=US/ST=California/L=Santa Barbara/O=Bitwarden Inc./OU=Bitwarden/CN=${BW_DOMAIN:-localhost}"
fi

# Launch a loop to rotate nginx logs on a daily basis
/bin/sh -c "/logrotate.sh loop >/dev/null 2>&1 &"

/usr/local/bin/hbs

# Enable/Disable services
sed -i "s/autostart=true/autostart=${BW_ENABLE_ADMIN}/" /etc/supervisor.d/admin.ini
sed -i "s/autostart=true/autostart=${BW_ENABLE_API}/" /etc/supervisor.d/api.ini
sed -i "s/autostart=true/autostart=${BW_ENABLE_EVENTS}/" /etc/supervisor.d/events.ini
sed -i "s/autostart=true/autostart=${BW_ENABLE_ICONS}/" /etc/supervisor.d/icons.ini
sed -i "s/autostart=true/autostart=${BW_ENABLE_IDENTITY}/" /etc/supervisor.d/identity.ini
sed -i "s/autostart=true/autostart=${BW_ENABLE_NOTIFICATIONS}/" /etc/supervisor.d/notifications.ini
sed -i "s/autostart=true/autostart=${BW_ENABLE_SCIM}/" /etc/supervisor.d/scim.ini
sed -i "s/autostart=true/autostart=${BW_ENABLE_SSO}/" /etc/supervisor.d/sso.ini

exec /usr/bin/supervisord
