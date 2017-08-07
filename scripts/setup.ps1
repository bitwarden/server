param (
  [string]$outputDir = "c:/bitwarden",
  [string]$domain = $( Read-Host "Please enter your domain name (i.e. bitwarden.company.com)" ),
  [string]$email = $( Read-Host "Please enter your email address (used to generate an HTTPS certificate with LetsEncrypt)" )
)

$dockerDir="../docker"
$certPassword=-join ((48..57) + (97..122) | Get-Random -Count 32 | % {[char]$_})
$databasePassword=-join ((48..57) + (97..122) | Get-Random -Count 32 | % {[char]$_})
$duoKey=-join ((48..57) + (97..122) | Get-Random -Count 32 | % {[char]$_})

docker --version

#mkdir -p $outputDir/letsencrypt/live/$domain
#docker run -it --rm -p 80:80 -v $outputDir/letsencrypt:/etc/letsencrypt/ certbot/certbot certonly --standalone --noninteractive --preferred-challenges http --email $email --agree-tos -d $domain
#docker run -it --rm -v $outputDir/letsencrypt/live:/certificates/ bitwarden/openssl openssl dhparam -out /certificates/$domain/dhparam.pem 2048

mkdir -p $outputDir/core
docker run -it --rm -v $outputDir/core:/certificates bitwarden/openssl openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout /certificates/identity.key -out /certificates/identity.crt -subj "/CN=bitwarden IdentityServer" -days 10950
docker run -it --rm -v $outputDir/core:/certificates bitwarden/openssl openssl pkcs12 -export -out /certificates/identity.pfx -inkey /certificates/identity.key -in /certificates/identity.crt -certfile /certificates/identity.crt -passout pass:$certPassword
rm $outputDir/core/identity.key
rm $outputDir/core/identity.crt

Add-Content $dockerDir/global.override.env "
globalSettings:baseServiceUri:vault=https://$domain
globalSettings:baseServiceUri:api=https://$domain/api
globalSettings:baseServiceUri:identity=https://$domain/identity
globalSettings:sqlServer:connectionString=Server=tcp:mssql,1433;Initial Catalog=vault;Persist Security Info=False;User ID=sa;Password=$databasePassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Connection Timeout=30;
globalSettings:identityServer:certificatePassword=$certPassword
globalSettings:duo:aKey=$duoKey
globalSettings:yubico:clientId=REPLACE
globalSettings:yubico:REPLACE"

Add-Content $dockerDir/mssql.override.env "
ACCEPT_EULA=Y
MSSQL_PID=Express
SA_PASSWORD=$databasePassword"
