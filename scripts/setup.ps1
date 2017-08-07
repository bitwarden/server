param (
  [string]$outputDir = "c:/bitwarden",
  [string]$domain = $( Read-Host "Please enter your domain name (i.e. bitwarden.company.com)" ),
  [string]$email = $( Read-Host "Please enter your email address: " ),
  [string]$letsencrypt = $( Read-Host "Generate Let's Encrypt Cert (y/n)" )
)

$dockerDir="../docker"
$databasePassword=-join ((48..57) + (97..122) | Get-Random -Count 32 | % {[char]$_})

docker --version

#mkdir -p $outputDir/letsencrypt/live/$domain
#docker run -it --rm -p 80:80 -v $outputDir/letsencrypt:/etc/letsencrypt/ certbot/certbot certonly --standalone --noninteractive --preferred-challenges http --email $email --agree-tos -d $domain
#docker run -it --rm -v $outputDir/letsencrypt/live:/certificates/ bitwarden/openssl openssl dhparam -out /certificates/$domain/dhparam.pem 2048

docker run -it --rm -v ${outputDir}:/bitwarden bitwarden/setup dotnet Setup.dll -domain ${domain} -letsencrypt ${letsencrypt} -db_pass ${databasePassword}

echo "Setup complete"
