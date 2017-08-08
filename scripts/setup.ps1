param (
    [string]$outputDir = "c:/bitwarden",
    [string]$domain = $( Read-Host "Enter your domain name (i.e. bitwarden.company.com)" ),
    [string]$email = $( Read-Host "Enter your email address" ),
    [string]$letsencrypt = $( Read-Host "Do you want to use Let's Encrypt to generate a free SSL certificate? (y/n)" )
)

docker --version

$dockerDir="../docker"
$databasePassword=-join ((48..57) + (97..122) | Get-Random -Count 32 | % {[char]$_})

if($letsencrypt -eq "y") {
    mkdir -p $outputDir/letsencrypt/live/$domain
    docker run -it --rm -p 80:80 -v $outputDir/letsencrypt:/etc/letsencrypt/ certbot/certbot certonly --standalone --noninteractive --preferred-challenges http --email $email --agree-tos -d $domain
}

docker run -it --rm -v ${outputDir}:/bitwarden bitwarden/setup dotnet Setup.dll -domain ${domain} -letsencrypt ${letsencrypt} -db_pass ${databasePassword}

echo "Setup complete"
